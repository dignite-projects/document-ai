using System;
using System.Linq;
using Shouldly;
using UglyToad.PdfPig;
using Xunit;

namespace Dignite.Vault.Extract.Parse.Pdf;

/// <summary>
/// End-to-end tests for the #450 lattice path through <see cref="PdfReadingOrder.RenderPage"/> — a PDF whose
/// table draws its grid with vector rules is reconstructed from that grid, exactly as <c>PdfExtractor</c> drives
/// it (words + the page's ruling-line bounds).
/// </summary>
public class PdfLatticeIntegration_Tests
{
    private static string RenderFirstPage(byte[] pdf)
    {
        using var doc = PdfDocument.Open(pdf);
        var page = doc.GetPage(1);
        var words = page.GetWords().ToList();
        var rulingBounds = page.Paths
            .SelectMany(p => p)
            .Select(sp => sp.GetBoundingRectangle())
            .Where(r => r.HasValue)
            .Select(r => r!.Value)
            .ToList();

        return PdfReadingOrder.RenderPage(
            words, Array.Empty<PdfReadingOrder.Figure>(), true, PdfHeadingScale.Build(words), rulingBounds);
    }

    [Fact]
    public void Reconstructs_a_ruled_table_from_its_drawn_grid()
    {
        // A 3-column x 2-row table whose grid is DRAWN. The middle column is empty on the data row — a sparse
        // column the whitespace path could drop — but the drawn grid keeps all three columns.
        var pdf = PdfFixtures.BuildWithRules(
            texts: new[]
            {
                ("Name", 60.0, 635.0), ("Qty", 160.0, 635.0), ("Note", 260.0, 635.0), // header row (band 630..660)
                ("Apple", 60.0, 605.0), ("red", 260.0, 605.0)                          // data row (band 600..630); Qty empty
            },
            verticalRules: new[]
            {
                (50.0, 600.0, 660.0), (150.0, 600.0, 660.0), (250.0, 600.0, 660.0), (350.0, 600.0, 660.0)
            },
            horizontalRules: new[]
            {
                (600.0, 50.0, 350.0), (630.0, 50.0, 350.0), (660.0, 50.0, 350.0)
            });

        RenderFirstPage(pdf).ShouldBe(
            "| Name | Qty | Note |\n| --- | --- | --- |\n| Apple |  | red |");
    }

    [Fact]
    public void Falls_back_to_the_stream_path_when_no_grid_is_drawn()
    {
        // The same table with NO ruling lines: the lattice path finds no grid and the whitespace/stream path
        // reconstructs it (all columns filled here so it reads as a clean grid).
        var pdf = PdfFixtures.BuildWithRules(
            texts: new[]
            {
                ("Name", 60.0, 635.0), ("Qty", 160.0, 635.0), ("Note", 260.0, 635.0),
                ("Apple", 60.0, 605.0), ("5", 160.0, 605.0), ("red", 260.0, 605.0)
            },
            verticalRules: Array.Empty<(double, double, double)>(),
            horizontalRules: Array.Empty<(double, double, double)>());

        RenderFirstPage(pdf).ShouldBe(
            "| Name | Qty | Note |\n| --- | --- | --- |\n| Apple | 5 | red |");
    }
}
