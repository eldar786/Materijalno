using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace Materijalno.UI.ValidationRules
{
    public class FormValidationRule : ValidationRule
    {

        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            //Ukoliko zelimo validaciju po tipu propertija (string ili int)
            Regex regex = new Regex("^[A-Za-z0-9 !@#$%^&]{0,40}$");

            if (!regex.IsMatch(value.ToString()))
            {
                return new ValidationResult(false, "Molimo unesite ispravno u polje");
            }
            if (value == null || value.ToString() == "")
            {
                return new ValidationResult(false, "Ne može biti prazno polje");
            }
            else
            {
                return ValidationResult.ValidResult;
                //return new ValidationResult(false, "Obratite pažnju na slova i brojeve");
            }
        }
    }
}