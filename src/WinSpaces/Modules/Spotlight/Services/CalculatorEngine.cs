using NCalc;
using WinSpaces.Diagnostics;

namespace WinSpaces.Modules.Spotlight.Services;

/// <summary>
/// Moteur de calcul mathématique utilisant NCalc.
/// Évalue les expressions saisies (ex: "2+2", "sqrt(16)", "10 * 5").
/// </summary>
public sealed class CalculatorEngine : HealthCheckableBase
{
    public override string ComponentName => "Calculator Engine";
    public override bool IsHealthy => true;
    public override string StatusMessage => "Moteur de calcul actif";

    public CalculatorEngine()
    {
        SetMetric("calculations_count", 0);
    }

    /// <summary>
    /// Essaie d'évaluer une expression mathématique.
    /// Retourne null si l'expression n'est pas valide.
    /// </summary>
    public CalculationResult? TryEvaluate(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression)) return null;

        // Détection rapide : doit contenir au moins un chiffre ou une fonction mathématique
        if (!ContainsMathPattern(expression)) return null;

        try
        {
            var expr = new Expression(expression);
            var result = expr.Evaluate();
            
            if (result == null) return null;

            // Incrémenter le compteur
            var count = (int)(Metrics.GetValueOrDefault("calculations_count", 0));
            SetMetric("calculations_count", count + 1);
            SetMetric("last_calculation", expression);

            return new CalculationResult
            {
                Expression = expression,
                Result = Convert.ToDouble(result),
                FormattedResult = FormatResult(Convert.ToDouble(result))
            };
        }
        catch (Exception)
        {
            // Expression invalide, pas une erreur critique
            return null;
        }
    }

    private static bool ContainsMathPattern(string text)
    {
        // Doit contenir au moins un chiffre
        if (!text.Any(char.IsDigit)) return false;

        // ET au moins un opérateur ou une fonction mathématique
        char[] operators = { '+', '-', '*', '/', '^', '(', ')' };
        string[] functions = { "sqrt", "sin", "cos", "tan", "log", "abs", "pow" };

        return operators.Any(text.Contains) || 
               functions.Any(f => text.Contains(f, StringComparison.OrdinalIgnoreCase));
    }

    private static string FormatResult(double value)
    {
        // Si entier, afficher sans décimales
        if (Math.Abs(value - Math.Round(value)) < 0.0000001)
            return Math.Round(value).ToString("N0");

        // Sinon, 4 décimales max
        return value.ToString("F4").TrimEnd('0').TrimEnd('.');
    }
}

public record CalculationResult
{
    public required string Expression { get; init; }
    public required double Result { get; init; }
    public required string FormattedResult { get; init; }
}
