// --- FULLY CORRECTED AND FINALIZED: Controllers/StockMovementController.cs ---
using IMS_Group03.Models;
using IMS_Group03.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace IMS_Group03.Controllers
{
    public class StockMovementController : INotifyPropertyChanged
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<StockMovementController> _logger;
        private int? _currentUserId;

        #region Properties (With Fixes for Dropdowns)

        public ObservableCollection<StockMovement> StockMovements { get; } = new();
        public ObservableCollection<Product> AvailableProducts { get; } = new();

        private Product? _selectedProductFilter;
        public Product? SelectedProductFilter
        {
            get => _selectedProductFilter;
            set
            {
                if (_selectedProductFilter != value)
                {
                    _selectedProductFilter = value;
                    OnPropertyChanged();
                    if (_selectedProductFilter != null && _selectedProductFilter.Id > 0)
                    {
                        _ = LoadMovementsForProductAsync(_selectedProductFilter.Id);
                    }
                    else
                    {
                        StockMovements.Clear();
                    }
                }
            }
        }

        private int _adjustmentProductId;
        public int AdjustmentProductId
        {
            get => _adjustmentProductId;
            set { if (_adjustmentProductId != value) { _adjustmentProductId = value; OnPropertyChanged(); } }
        }

        public string AdjustmentNewQuantityInput { get; set; } = "0";
        public string AdjustmentReason { get; set; } = string.Empty;
        public bool IsBusy { get; private set; }
        public string ErrorMessage { get; private set; } = string.Empty;
        public event PropertyChangedEventHandler? PropertyChanged;
        #endregion

        public StockMovementController(IServiceScopeFactory scopeFactory, ILogger<StockMovementController> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public void SetCurrentUser(User user)
        {
            _currentUserId = user.Id;
        }

        #region Loading and Preparation Methods (Correct)

        public async Task LoadInitialDataAsync()
        {
            IsBusy = true; OnPropertyChanged(nameof(IsBusy));
            using (var scope = _scopeFactory.CreateScope())
            {
                try
                {
                    var productService = scope.ServiceProvider.GetRequiredService<IProductService>();
                    var products = await productService.GetAllProductsAsync();
                    AvailableProducts.Clear();
                    AvailableProducts.Add(new Product { Id = 0, Name = "-- Select a Product to Filter --" });
                    foreach (var p in products.OrderBy(p => p.Name)) AvailableProducts.Add(p);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load products for stock movement view.");
                    ErrorMessage = "Could not load product list.";
                }
                finally { IsBusy = false; OnAllPropertiesChanged(); }
            }
        }

        public async Task LoadMovementsForProductAsync(int productId)
        {
            IsBusy = true; OnPropertyChanged(nameof(IsBusy));
            using (var scope = _scopeFactory.CreateScope())
            {
                try
                {
                    var stockMovementService = scope.ServiceProvider.GetRequiredService<IStockMovementService>();
                    var movements = await stockMovementService.GetMovementsForProductAsync(productId);
                    StockMovements.Clear();
                    foreach (var m in movements) StockMovements.Add(m);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load movements for product {ProductId}", productId);
                    ErrorMessage = "Could not load movement history.";
                }
                finally { IsBusy = false; OnAllPropertiesChanged(); }
            }
        }

        public void ClearAdjustmentForm()
        {
            AdjustmentProductId = 0;
            AdjustmentNewQuantityInput = "0"; OnPropertyChanged(nameof(AdjustmentNewQuantityInput));
            AdjustmentReason = string.Empty; OnPropertyChanged(nameof(AdjustmentReason));
        }
        #endregion

        #region Stock Adjustment (This is the corrected method)

        public async Task<(bool Success, string Message)> PerformStockAdjustmentAsync()
        {
            if (_currentUserId == null)
            {
                return (false, "Critical error: No user session found. Please log out and log back in.");
            }
            if (AdjustmentProductId == 0) return (false, "Please select a product to adjust.");
            if (!int.TryParse(AdjustmentNewQuantityInput, out int actualNewQuantity) || actualNewQuantity < 0)
                return (false, "New quantity must be a valid non-negative number.");
            if (string.IsNullOrWhiteSpace(AdjustmentReason)) return (false, "Adjustment reason is required.");

            IsBusy = true; ErrorMessage = string.Empty; OnAllPropertiesChanged();

            using (var scope = _scopeFactory.CreateScope())
            {
                try
                {
                    // FIX: The service is resolved and used within the same block.
                    var stockMovementService = scope.ServiceProvider.GetRequiredService<IStockMovementService>();

                    await stockMovementService.RecordStockAdjustmentAsync(
                        AdjustmentProductId,
                        actualNewQuantity,
                        AdjustmentReason,
                        _currentUserId
                    );

                    await LoadMovementsForProductAsync(AdjustmentProductId);

                    var productInList = AvailableProducts.FirstOrDefault(p => p.Id == AdjustmentProductId);
                    if (productInList != null)
                    {
                        // Manually update the in-memory collection so the user sees the change instantly.
                        productInList.QuantityInStock = actualNewQuantity;
                    }

                    ClearAdjustmentForm();
                    return (true, "Stock adjusted successfully.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to perform stock adjustment for Product ID {ProductId}", AdjustmentProductId);
                    ErrorMessage = $"An unexpected error occurred during the adjustment.";
                    return (false, ErrorMessage);
                }
                finally { IsBusy = false; OnAllPropertiesChanged(); }
            }
        }
        #endregion

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void OnAllPropertiesChanged()
        {
            OnPropertyChanged(nameof(IsBusy));
            OnPropertyChanged(nameof(ErrorMessage));
        }
    }
}