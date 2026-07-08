using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Effects;
using ProjektProgramowanie.Models;
using ProjektProgramowanie.Repositories;

namespace ProjektProgramowanie
{
    public partial class MainWindow : Window
    {
        private readonly IRiskRepository _repository;
        public ObservableCollection<RiskItem> Risks { get; set; }

        private ICollectionView _risksView;
        private RiskItem _riskBeingEdited = null;
        private bool _hasUnsavedChanges = false;

        // Spójna paleta kolorów używana zarówno w macierzy, jak i w komunikacie statusu.
        private static readonly Color ColorLow = (Color)ColorConverter.ConvertFromString("#FF81C784");
        private static readonly Color ColorMedium = (Color)ColorConverter.ConvertFromString("#FFFFD54F");
        private static readonly Color ColorHigh = (Color)ColorConverter.ConvertFromString("#FFE57373");

        // Współrzędne Rzeszowa – używane do zapytania do Open-Meteo API.
        private const double RzeszowLatitude = 50.0412;
        private const double RzeszowLongitude = 21.9991;

        private static readonly HttpClient _httpClient = new HttpClient();

        public MainWindow()
        {
            InitializeComponent();
            _repository = new JsonRiskRepository();

            var loadedRisks = _repository.GetAllRisks();
            Risks = new ObservableCollection<RiskItem>(loadedRisks);
            Risks.CollectionChanged += Risks_CollectionChanged;

            foreach (var risk in Risks)
            {
                risk.PropertyChanged += Risk_PropertyChanged;
            }

            _risksView = CollectionViewSource.GetDefaultView(Risks);
            _risksView.Filter = FilterByStatus;
            DgRisks.ItemsSource = _risksView;

            CmbCategory.ItemsSource = Enum.GetValues(typeof(RiskCategory));
            CmbCategory.SelectedIndex = 0;
            CmbStatus.ItemsSource = Enum.GetValues(typeof(RiskStatus));
            CmbStatus.SelectedIndex = 0;

            SetupStatusFilter();
            RefreshRiskVisuals();
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadWeatherAsync();
        }

        /// <summary>
        /// Pobiera aktualną pogodę dla Rzeszowa z darmowego API Open-Meteo
        /// (bez klucza, bez rejestracji) i aktualizuje widget pogodowy.
        /// Jeśli temperatura jest ekstremalna albo warunki są niebezpieczne
        /// (burza, oblodzenie, intensywny śnieg), pokazuje dodatkowe ostrzeżenie
        /// nawiązujące do ryzyka operacyjnego dla infrastruktury IT.
        /// </summary>
        private async Task LoadWeatherAsync()
        {
            try
            {
                string url = $"https://api.open-meteo.com/v1/forecast?latitude={RzeszowLatitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}&longitude={RzeszowLongitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}&current_weather=true";

                string json = await _httpClient.GetStringAsync(url);

                using var document = JsonDocument.Parse(json);
                var current = document.RootElement.GetProperty("current_weather");

                double temperature = current.GetProperty("temperature").GetDouble();
                double windSpeed = current.GetProperty("windspeed").GetDouble();
                int weatherCode = current.GetProperty("weathercode").GetInt32();

                var (icon, description, isSevere) = GetWeatherDescription(weatherCode, temperature);

                TxtWeatherIcon.Text = icon;
                TxtWeatherTemp.Text = $"{temperature:0.#}°C";
                TxtWeatherDescription.Text = description;
                TxtWeatherWind.Text = $"Wiatr: {windSpeed:0.#} km/h";

                if (isSevere)
                {
                    TxtWeatherWarning.Text = "⚠️ Warunki pogodowe mogą zwiększać ryzyko operacyjne dla infrastruktury IT.";
                    TxtWeatherWarning.Visibility = Visibility.Visible;
                }
                else
                {
                    TxtWeatherWarning.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception)
            {
                // Brak internetu, API niedostępne itp. – nie przerywamy działania aplikacji.
                TxtWeatherIcon.Text = "❓";
                TxtWeatherTemp.Text = "--";
                TxtWeatherDescription.Text = "Nie udało się pobrać danych pogodowych.";
                TxtWeatherWind.Text = string.Empty;
                TxtWeatherWarning.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Tłumaczy kod pogodowy Open-Meteo (WMO Weather Code) na ikonę,
        /// polski opis oraz flagę wskazującą, czy warunki są "surowe"
        /// (mogące potencjalnie zwiększać ryzyko operacyjne).
        /// </summary>
        private (string icon, string description, bool isSevere) GetWeatherDescription(int code, double temperature)
        {
            string icon = code switch
            {
                0 => "☀️",
                1 or 2 => "🌤️",
                3 => "☁️",
                45 or 48 => "🌫️",
                51 or 53 or 55 => "🌦️",
                56 or 57 => "🌧️",
                61 or 63 or 65 => "🌧️",
                66 or 67 => "🧊",
                71 or 73 or 75 or 77 => "❄️",
                80 or 81 or 82 => "🌧️",
                85 or 86 => "🌨️",
                95 or 96 or 99 => "⛈️",
                _ => "🌡️"
            };

            string description = code switch
            {
                0 => "Bezchmurnie",
                1 => "Przeważnie bezchmurnie",
                2 => "Częściowe zachmurzenie",
                3 => "Pochmurno",
                45 or 48 => "Mgła",
                51 or 53 or 55 => "Mżawka",
                56 or 57 => "Marznąca mżawka",
                61 => "Słaby deszcz",
                63 => "Umiarkowany deszcz",
                65 => "Silny deszcz",
                66 or 67 => "Marznący deszcz",
                71 => "Słaby śnieg",
                73 => "Umiarkowany śnieg",
                75 => "Silny śnieg",
                77 => "Ziarna śniegu",
                80 or 81 or 82 => "Przelotne opady deszczu",
                85 or 86 => "Przelotne opady śniegu",
                95 => "Burza",
                96 or 99 => "Burza z gradem",
                _ => "Nieznane warunki"
            };

            bool severeByCode = code is 56 or 57 or 66 or 67 or 71 or 73 or 75 or 77
                                       or 85 or 86 or 95 or 96 or 99;
            bool severeByTemp = temperature <= -10 || temperature >= 35;

            return (icon, description, severeByCode || severeByTemp);
        }

        private void SetupStatusFilter()
        {
            var options = new System.Collections.Generic.List<string> { "Wszystkie" };
            options.AddRange(Enum.GetNames(typeof(RiskStatus)));
            CmbFilterStatus.ItemsSource = options;
            CmbFilterStatus.SelectedIndex = 0;
        }

        private bool FilterByStatus(object item)
        {
            if (CmbFilterStatus.SelectedItem == null) return true;
            string selected = CmbFilterStatus.SelectedItem.ToString();
            if (selected == "Wszystkie") return true;

            var risk = item as RiskItem;
            return risk != null && risk.Status.ToString() == selected;
        }

        private void CmbFilterStatus_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _risksView?.Refresh();
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtName.Text))
            {
                MessageBox.Show("Podaj nazwę ryzyka przed dodaniem!", "Brak danych", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_riskBeingEdited != null)
            {
                _riskBeingEdited.Name = TxtName.Text;
                _riskBeingEdited.Owner = TxtOwner.Text;
                _riskBeingEdited.Category = (RiskCategory)CmbCategory.SelectedItem;
                _riskBeingEdited.Status = (RiskStatus)CmbStatus.SelectedItem;
                _riskBeingEdited.Probability = (int)SldProbability.Value;
                _riskBeingEdited.Impact = (int)SldImpact.Value;

                _riskBeingEdited = null;
                BtnAdd.Content = "Dodaj Ryzyko do Listy";
                TxtEditingInfo.Visibility = Visibility.Collapsed;

                RefreshRiskVisuals();
                _hasUnsavedChanges = true;
            }
            else
            {
                var newRisk = new RiskItem
                {
                    Name = TxtName.Text,
                    Owner = TxtOwner.Text,
                    Category = (RiskCategory)CmbCategory.SelectedItem,
                    Status = (RiskStatus)CmbStatus.SelectedItem,
                    Probability = (int)SldProbability.Value,
                    Impact = (int)SldImpact.Value
                };

                Risks.Add(newRisk);
                _hasUnsavedChanges = true;
            }

            ClearForm();
        }

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (DgRisks.SelectedItem is RiskItem selectedRisk)
            {
                _riskBeingEdited = selectedRisk;

                TxtName.Text = selectedRisk.Name;
                TxtOwner.Text = selectedRisk.Owner;
                CmbCategory.SelectedItem = selectedRisk.Category;
                CmbStatus.SelectedItem = selectedRisk.Status;
                SldProbability.Value = selectedRisk.Probability;
                SldImpact.Value = selectedRisk.Impact;

                BtnAdd.Content = "Zapisz zmiany";
                TxtEditingInfo.Text = $"Edytujesz: \"{selectedRisk.Name}\". Kliknij \"Zapisz zmiany\" lub \"Anuluj edycję\".";
                TxtEditingInfo.Visibility = Visibility.Visible;
            }
            else
            {
                MessageBox.Show("Zaznacz najpierw wiersz w tabeli, który chcesz edytować.", "Brak zaznaczenia", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnCancelEdit_Click(object sender, RoutedEventArgs e)
        {
            _riskBeingEdited = null;
            BtnAdd.Content = "Dodaj Ryzyko do Listy";
            TxtEditingInfo.Visibility = Visibility.Collapsed;
            ClearForm();
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (DgRisks.SelectedItem is RiskItem selectedRisk)
            {
                var result = MessageBox.Show(
                    $"Czy na pewno chcesz usunąć ryzyko \"{selectedRisk.Name}\"?",
                    "Potwierdzenie usunięcia",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    if (_riskBeingEdited == selectedRisk)
                    {
                        BtnCancelEdit_Click(sender, e);
                    }

                    Risks.Remove(selectedRisk);
                    _hasUnsavedChanges = true;
                }
            }
            else
            {
                MessageBox.Show("Zaznacz najpierw wiersz w tabeli, który chcesz usunąć.", "Brak zaznaczenia", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            SaveRisks();
        }

        private bool SaveRisks()
        {
            bool success = _repository.SaveAllRisks(Risks.ToList());
            if (success)
            {
                _hasUnsavedChanges = false;
                MessageBox.Show("Stan macierzy ryzyka został zapisany poprawnie do pliku JSON.", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            return success;
        }

        private void ClearForm()
        {
            TxtName.Text = string.Empty;
            TxtOwner.Text = string.Empty;
            SldProbability.Value = 1;
            SldImpact.Value = 1;
        }

        private void Risks_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (RiskItem item in e.NewItems)
                {
                    item.PropertyChanged += Risk_PropertyChanged;
                }
            }

            if (e.OldItems != null)
            {
                foreach (RiskItem item in e.OldItems)
                {
                    item.PropertyChanged -= Risk_PropertyChanged;
                }
            }

            RefreshRiskVisuals();
        }

        private void Risk_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(RiskItem.Probability) || e.PropertyName == nameof(RiskItem.Impact))
            {
                RefreshRiskVisuals();
            }
        }

        private void RefreshRiskVisuals()
        {
            BuildRiskMatrix();
            UpdateRiskStatusMessage();
        }

        private void BuildRiskMatrix()
        {
            RiskMatrixGrid.Children.Clear();
            RiskMatrixGrid.RowDefinitions.Clear();
            RiskMatrixGrid.ColumnDefinitions.Clear();

            for (int i = 0; i < 6; i++)
            {
                RiskMatrixGrid.RowDefinitions.Add(new RowDefinition());
                RiskMatrixGrid.ColumnDefinitions.Add(new ColumnDefinition());
            }

            for (int impact = 1; impact <= 5; impact++)
            {
                var header = new TextBlock
                {
                    Text = impact.ToString(),
                    FontWeight = FontWeights.Bold,
                    FontSize = 13,
                    Foreground = Brushes.DimGray,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetRow(header, 0);
                Grid.SetColumn(header, impact);
                RiskMatrixGrid.Children.Add(header);
            }

            for (int probability = 1; probability <= 5; probability++)
            {
                int gridRow = 6 - probability;
                var header = new TextBlock
                {
                    Text = probability.ToString(),
                    FontWeight = FontWeights.Bold,
                    FontSize = 13,
                    Foreground = Brushes.DimGray,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetRow(header, gridRow);
                Grid.SetColumn(header, 0);
                RiskMatrixGrid.Children.Add(header);
            }

            for (int probability = 1; probability <= 5; probability++)
            {
                int gridRow = 6 - probability;
                for (int impact = 1; impact <= 5; impact++)
                {
                    int score = probability * impact;
                    int count = Risks.Count(r => r.Probability == probability && r.Impact == impact);
                    bool hasRisks = count > 0;

                    var border = new Border
                    {
                        Background = new SolidColorBrush(GetRiskColor(score)),
                        BorderBrush = hasRisks ? Brushes.White : new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
                        BorderThickness = new Thickness(hasRisks ? 2 : 1),
                        CornerRadius = new CornerRadius(6),
                        Margin = new Thickness(2),
                        Opacity = hasRisks ? 1.0 : 0.55
                    };

                    if (hasRisks)
                    {
                        border.Effect = new DropShadowEffect
                        {
                            BlurRadius = 6,
                            ShadowDepth = 1,
                            Opacity = 0.35,
                            Color = Colors.Black
                        };
                    }

                    var text = new TextBlock
                    {
                        Text = hasRisks ? count.ToString() : string.Empty,
                        FontWeight = FontWeights.Bold,
                        FontSize = 15,
                        Foreground = GetTextColorForScore(score),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    border.Child = text;
                    Grid.SetRow(border, gridRow);
                    Grid.SetColumn(border, impact);
                    RiskMatrixGrid.Children.Add(border);
                }
            }
        }

        private Color GetRiskColor(int score)
        {
            if (score <= 4) return ColorLow;
            if (score <= 10) return ColorMedium;
            return ColorHigh;
        }

        private Brush GetTextColorForScore(int score)
        {
            return score <= 10 ? Brushes.Black : Brushes.White;
        }

        private void UpdateRiskStatusMessage()
        {
            if (Risks.Count == 0)
            {
                SetStatusMessage(
                    Color.FromRgb(0xE0, 0xE0, 0xE0),
                    Brushes.Black,
                    "Brak zarejestrowanych ryzyk",
                    "Dodaj pierwsze ryzyko, aby zobaczyć ocenę stanu bezpieczeństwa.");
                return;
            }

            int maxScore = Risks.Max(r => r.Score);

            if (maxScore <= 4)
            {
                SetStatusMessage(
                    ColorLow,
                    Brushes.Black,
                    "🟢 Ryzyko opanowane",
                    "Wszystkie zidentyfikowane zagrożenia znajdują się na niskim poziomie. Utrzymuj bieżący monitoring.");
            }
            else if (maxScore <= 10)
            {
                SetStatusMessage(
                    ColorMedium,
                    Brushes.Black,
                    "🟡 Wymagane wdrożenie poprawek",
                    "Wykryto ryzyka na poziomie średnim. Zalecane jest zaplanowanie i wdrożenie działań ograniczających.");
            }
            else
            {
                SetStatusMessage(
                    ColorHigh,
                    Brushes.White,
                    "🔴 Ryzyko krytyczne",
                    "Wykryto ryzyka wysokie lub krytyczne. Zmiany naprawcze należy wdrożyć natychmiast.");
            }
        }

        private void SetStatusMessage(Color backgroundColor, Brush textBrush, string title, string detail)
        {
            BorderRiskStatus.Background = new SolidColorBrush(backgroundColor);
            TxtRiskStatusTitle.Foreground = textBrush;
            TxtRiskStatusDetail.Foreground = textBrush;
            TxtRiskStatusTitle.Text = title;
            TxtRiskStatusDetail.Text = detail;
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (!_hasUnsavedChanges) return;

            var result = MessageBox.Show(
                "Masz niezapisane zmiany. Czy chcesz je zapisać przed zamknięciem programu?",
                "Niezapisane zmiany",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                bool success = SaveRisks();
                if (!success)
                {
                    e.Cancel = true;
                }
            }
            else if (result == MessageBoxResult.Cancel)
            {
                e.Cancel = true;
            }
        }
    }
}