using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;
using ProjektProgramowanie.Models;

namespace ProjektProgramowanie.Repositories
{
    /// <summary>
    /// Implementacja IRiskRepository zapisująca dane w pliku JSON na dysku.
    /// </summary>
    public class JsonRiskRepository : IRiskRepository
    {
        private readonly string _filePath = "risks.json";

        public List<RiskItem> GetAllRisks()
        {
            if (!File.Exists(_filePath))
            {
                return new List<RiskItem>();
            }

            try
            {
                string jsonString = File.ReadAllText(_filePath);
                var risks = JsonSerializer.Deserialize<List<RiskItem>>(jsonString);
                return risks ?? new List<RiskItem>();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Nie udało się wczytać pliku risks.json.\nZostanie utworzona nowa, pusta lista.\n\nSzczegóły: {ex.Message}",
                    "Błąd odczytu danych",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return new List<RiskItem>();
            }
        }

        public bool SaveAllRisks(List<RiskItem> risks)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string jsonString = JsonSerializer.Serialize(risks, options);
                File.WriteAllText(_filePath, jsonString);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Nie udało się zapisać danych do pliku risks.json.\n\nSzczegóły: {ex.Message}",
                    "Błąd zapisu danych",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
        }
    }
}