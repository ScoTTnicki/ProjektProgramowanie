using System.Collections.Generic;
using ProjektProgramowanie.Models;

namespace ProjektProgramowanie.Repositories
{
    /// <summary>
    /// Definiuje operacje odczytu i zapisu listy ryzyk w wybranym magazynie danych.
    /// </summary>
    public interface IRiskRepository
    {
        /// <summary>
        /// Wczytuje wszystkie zapisane ryzyka. W przypadku błędu odczytu
        /// zwraca pustą listę zamiast zgłaszać wyjątek.
        /// </summary>
        List<RiskItem> GetAllRisks();

        /// <summary>
        /// Zapisuje pełną listę ryzyk do magazynu danych.
        /// </summary>
        /// <returns>True, jeśli zapis się powiódł; false w przypadku błędu.</returns>
        bool SaveAllRisks(List<RiskItem> risks);
    }
}