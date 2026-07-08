using System;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace ProjektProgramowanie.Models
{
    public enum RiskCategory
    {
        Techniczne,
        Organizacyjne,
        Zgodność,
        Bezpieczeństwo
    }

    public enum RiskStatus
    {
        Otwarte,
        Realizowane,
        Zaakceptowane,
        Rozwiązane
    }

    /// <summary>
    /// Reprezentuje pojedynczy wpis ryzyka w macierzy ryzyka IT.
    /// Implementuje INotifyPropertyChanged, aby zmiany właściwości
    /// (np. podczas edycji istniejącego wpisu) były natychmiast widoczne
    /// w DataGrid oraz mogły odświeżać macierz wizualną.
    /// </summary>
    public class RiskItem : INotifyPropertyChanged
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        private string _name;
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(nameof(Name)); }
        }

        private string _owner;
        public string Owner
        {
            get => _owner;
            set { _owner = value; OnPropertyChanged(nameof(Owner)); }
        }

        private RiskCategory _category;
        public RiskCategory Category
        {
            get => _category;
            set { _category = value; OnPropertyChanged(nameof(Category)); }
        }

        private RiskStatus _status;
        public RiskStatus Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(nameof(Status)); }
        }

        private int _probability;
        public int Probability
        {
            get => _probability;
            set
            {
                _probability = value;
                OnPropertyChanged(nameof(Probability));
                OnPropertyChanged(nameof(Score));
                OnPropertyChanged(nameof(RiskLevel));
            }
        }

        private int _impact;
        public int Impact
        {
            get => _impact;
            set
            {
                _impact = value;
                OnPropertyChanged(nameof(Impact));
                OnPropertyChanged(nameof(Score));
                OnPropertyChanged(nameof(RiskLevel));
            }
        }

        /// <summary>
        /// Data utworzenia wpisu. Ustawiana automatycznie przy tworzeniu
        /// nowego ryzyka, nieedytowalna z poziomu formularza.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Wartość wyliczana — nie zapisujemy jej do JSON, bo i tak
        /// zostanie przeliczona na nowo przy wczytaniu pliku.
        /// </summary>
        [JsonIgnore]
        public int Score => Probability * Impact;

        /// <summary>
        /// Wartość wyliczana — jak wyżej, ignorowana przy serializacji.
        /// </summary>
        [JsonIgnore]
        public string RiskLevel
        {
            get
            {
                if (Score <= 4) return "Niskie";
                if (Score <= 10) return "Średnie";
                if (Score <= 15) return "Wysokie";
                return "Krytyczne";
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}