using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using demoModels;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace demo_14_4
{
    public partial class MainWindow : Window
    {
        ObservableCollection<ProductPresenter> products = new ObservableCollection<ProductPresenter>();
        ObservableCollection<string> productTypes;
        List<ProductPresenter> dataSours;

        private int currentPage = 1;
        private int itemsPerPage = 20;
        private int totalPages = 1;
        private List<ProductPresenter> filteredProducts = new List<ProductPresenter>();
        private List<ProductPresenter> selectedProducts = new List<ProductPresenter>();

        public MainWindow()
        {
            InitializeComponent();
            LoadData();
            InitializeUI();
        }

        private void LoadData()
        {
            using var context = new MydatabaseContext();

            dataSours = context.Products
                .Include(x => x.ProductMaterials)
                .ThenInclude(material => material.Material)
                .Where(it => it.ProductMaterials.Count > 0)
                .Include(type => type.ProductType)
                .Include(sale => sale.ProductSales)
                .Select(product => new ProductPresenter
                {
                    Id = product.Id,
                    Title = product.Title,
                    Images = product.Images,
                    ProductionWorkshopNumber = product.ProductionWorkshopNumber,
                    ArticleNumber = product.ArticleNumber,
                    MinCostForAgent = product.MinCostForAgent,
                    ProductMaterials = product.ProductMaterials,
                    ProductType = product.ProductType,
                    ProductSales = product.ProductSales
                })
                .ToList();

            var dataSourseType = context.ProductTypes.Select(it => it.Title).ToList();
            productTypes = new ObservableCollection<string>(dataSourseType);
            productTypes.Insert(0, "Все типы");
        }

        private void InitializeUI()
        {
            ProductBox.ItemsSource = products;
            FilterBox.ItemsSource = productTypes;
            SortBox.SelectedIndex = 0;
            FilterBox.SelectedIndex = 0;
            DisplayProducts();

            var changeCostButton = new Button
            {
                Content = "Изменить стоимость на...",
                Margin = new Avalonia.Thickness(5),
                IsEnabled = false
            };
            changeCostButton.Click += ChangeCostButton_Click;

            var toolPanel = this.FindControl<StackPanel>("ToolPanel");
            toolPanel.Children.Insert(1, changeCostButton);

            ProductBox.SelectionChanged += ProductBox_SelectionChanged;
        }

        public void ProductBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var listBox = (ListBox)sender;
            selectedProducts = listBox.SelectedItems.Cast<ProductPresenter>().ToList();

            var selectedCountText = this.FindControl<TextBlock>("SelectedCountText");
            selectedCountText.Text = $"Выбрано: {selectedProducts.Count}";

            var toolPanel = this.FindControl<StackPanel>("ToolPanel");
            var changeCostButton = toolPanel.Children.OfType<Button>().FirstOrDefault(b => b.Content.ToString().Contains("Изменить стоимость"));

            if (changeCostButton != null)
            {
                changeCostButton.IsEnabled = selectedProducts.Count > 0;
            }
        }

        public async void ChangeCostButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedProducts.Count == 0) return;

            double averageCost = selectedProducts.Average(p => (double)p.MinCostForAgent);

            var dialog = new ChangeCostDialog(averageCost);
            var result = await dialog.ShowDialog<bool>(this);

            if (result)
            {
                double newCost = dialog.NewCost;

                using var context = new MydatabaseContext();
                foreach (var product in selectedProducts)
                {
                    var dbProduct = context.Products.FirstOrDefault(p => p.Id == product.Id);
                    if (dbProduct != null)
                    {
                        dbProduct.MinCostForAgent = (decimal)newCost;
                        product.MinCostForAgent = (decimal)newCost;
                    }
                }

                context.SaveChanges();

                DisplayProducts();
            }
        }

        public void ProductItemClick(object sender, RoutedEventArgs e)
        {
            var product = ProductBox.SelectedItem as ProductPresenter;
            if (product == null) return;

            using var context = new MydatabaseContext();
            var productToDelete = context.Products
                .Include(p => p.ProductMaterials)
                .FirstOrDefault(p => p.Id == product.Id);

            if (productToDelete == null) return;

            context.ProductMaterials.RemoveRange(productToDelete.ProductMaterials);

            context.Products.Remove(productToDelete);

            if (context.SaveChanges() > 0)
            {
                products.Remove(product);
                DisplayProducts();
            }
        }

        private void DisplayProducts()
        {
            filteredProducts = dataSours.ToList();

            if (FilterBox.SelectedIndex > 0)
            {
                filteredProducts = filteredProducts.Where(it => it.TypeName.Contains(FilterBox.SelectedItem.ToString())).ToList();
            }

            if (!string.IsNullOrEmpty(SearchBox.Text))
            {
                var searchWord = SearchBox.Text.ToLower();
                filteredProducts = filteredProducts.Where(it => IsContains(it.Title, it.TypeName, searchWord)).ToList();
            }

            switch (SortBox.SelectedIndex)
            {
                case 1: filteredProducts = filteredProducts.OrderBy(p => p.Title).ToList(); break;
                case 2: filteredProducts = filteredProducts.OrderByDescending(p => p.Title).ToList(); break;
                case 3: filteredProducts = filteredProducts.OrderByDescending(p => p.ProductionWorkshopNumber).ToList(); break;
                case 4: filteredProducts = filteredProducts.OrderBy(p => p.ProductionWorkshopNumber).ToList(); break;
                case 5: filteredProducts = filteredProducts.OrderByDescending(p => p.MinCostForAgent).ToList(); break;
                case 6: filteredProducts = filteredProducts.OrderBy(p => p.MinCostForAgent).ToList(); break;
                default: break;
            }

            totalPages = (int)Math.Ceiling((double)filteredProducts.Count / itemsPerPage);
            if (currentPage > totalPages && totalPages > 0)
            {
                currentPage = totalPages;
            }
            else if (totalPages == 0)
            {
                currentPage = 1;
            }

            UpdatePaginationButtons();
            UpdateDisplayedProducts();
        }

        private void UpdateDisplayedProducts()
        {
            products.Clear();

            if (!filteredProducts.Any())
            {
                return;
            }

            var productsToDisplay = filteredProducts
                .Skip((currentPage - 1) * itemsPerPage)
                .Take(itemsPerPage)
                .ToList();

            foreach (var item in productsToDisplay)
            {
                products.Add(item);
            }
        }

        private void UpdatePaginationButtons()
        {
            PaginationPanel.Children.Clear();

            for (int i = 1; i <= totalPages; i++)
            {
                var button = new Button
                {
                    Content = i.ToString(),
                    Margin = new Avalonia.Thickness(5),
                    Width = 30,
                    Height = 30,
                    IsEnabled = i != currentPage
                };

                int page = i;
                button.Click += (s, e) =>
                {
                    currentPage = page;
                    UpdateDisplayedProducts();
                    UpdatePaginationButtons();
                };

                PaginationPanel.Children.Add(button);
            }
        }

        public bool IsContains(string title, string typeName, string searchWord)
        {
            string massage = (title + typeName).ToLower();
            searchWord = searchWord.ToLower();
            return massage.Contains(searchWord);
        }

        public void SearchBoxChanging(object sender, TextChangingEventArgs eventArgs)
        {
            currentPage = 1;
            DisplayProducts();
        }

        private void SortBox_SelectionChanged(object? sender, SelectionChangedEventArgs eventArgs)
        {
            currentPage = 1;
            DisplayProducts();
        }

        private void FilterBox_SelectionChanged(object? sender, SelectionChangedEventArgs eventArgs)
        {
            currentPage = 1;
            DisplayProducts();
        }
    }

    public class ProductPresenter() : Product
    {
        public string TypeName { get => ProductType.Title; }
        private static Bitmap? _defaultImage;

        public Bitmap? Image => GetImage();

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private Bitmap? GetImage()
        {
            try
            {
                if (!string.IsNullOrEmpty(Images)) return new Bitmap(Images);
            }
            catch { }

            return _defaultImage ??= LoadDefaultImage();
        }

        private static Bitmap LoadDefaultImage()
        {
            var defaultPath = Path.Combine("Images", "picture.png");
            return File.Exists(defaultPath)
                ? new Bitmap(defaultPath)
                : null;
        }

        public string BackgroundColor
        {
            get
            {
                if (!ProductSales.Any(s => s.SaleDate != null))
                {
                    return "Transparent";
                }

                var lastSaleDate = ProductSales
                    .Where(s => s.SaleDate != null)
                    .Max(s => s.SaleDate);

                if (lastSaleDate < DateOnly.FromDateTime(DateTime.Now.AddMonths(-1)))
                {
                    return "#d12e2e";
                }

                return "Transparent";
            }
        }
    }
}