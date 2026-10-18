namespace DocumentAssistant.Tests;

internal static class Samples
{
    public static string Read(string fileName) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "samples", fileName));

    public static string Antrag => Read("01_wohngeldantrag.txt");
    public static string Nachweis => Read("02_verdienstbescheinigung.txt");
    public static string Rechnung => Read("03_rechnung.txt");
}
