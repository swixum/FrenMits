using Xunit;

namespace FrenMits.Tests;

// The font sizing rules behind the overlay's crispness.
//
// A handle isn't ready the moment it's asked for - the atlas builds off-thread -
// so what matters is what gets drawn in the meantime. The old fallback magnified
// the ~12px bitmap atlas, which at the call overlay's default 40px was a 2.2x
// blow-up: the blocky first frames after pressing Test.
//
// FontManager itself needs Dalamud to build anything, so what's pinned here is
// the arithmetic that decides what gets drawn, which is where a mistake would
// show up as text at the wrong size rather than merely a soft one.
public class FontManagerTests
{
    // ---- the size grid ------------------------------------------------------

    [Theory]
    [InlineData(18f, 18)]
    [InlineData(19f, 20)]   // rounds to the 2px grid
    [InlineData(21f, 20)]   // .5 rounds to even (banker's), so this lands on 20
    [InlineData(22f, 22)]
    [InlineData(40f, 40)]
    public void SizesSnapToATwoPixelGrid(float asked, int got)
        => Assert.Equal(got, FontManager.SnapPx(asked));

    [Fact]
    public void EverySnappedSizeIsEven()
    {
        // The grid is what stops a slider notch building a brand new atlas on
        // every pixel: 12-120px would otherwise be 109 handles for one slider.
        for (var px = 8f; px <= 160f; px += 0.5f)
            Assert.Equal(0, FontManager.SnapPx(px) % 2);
    }

    [Theory]
    [InlineData(0f, 8)]
    [InlineData(-50f, 8)]
    [InlineData(9999f, 160)]
    public void NonsenseSizesAreClampedNotCrashed(float asked, int got)
        => Assert.Equal(got, FontManager.SnapPx(asked));

    [Fact]
    public void TheGridNeverMovesASizeByMoreThanAPixel()
    {
        for (var px = 8f; px <= 160f; px += 0.25f)
            Assert.True(MathF.Abs(FontManager.SnapPx(px) - px) <= 1f,
                $"{px} snapped to {FontManager.SnapPx(px)}");
    }

    // ---- borrowing a nearby handle ------------------------------------------

    [Fact]
    public void AnExactMatchIsNeverRescaled()
    {
        // Leaving the scale at exactly 1 keeps glyphs on whole pixels.
        Assert.Equal(1f, FontManager.Correction(40, 40));
    }

    [Fact]
    public void ACloseEnoughSizeIsLeftAlone()
    {
        // Under 2% out, correcting costs more than it fixes.
        Assert.Equal(1f, FontManager.Correction(40, 40));
        Assert.Equal(1f, FontManager.Correction(100, 101));
    }

    [Theory]
    [InlineData(40, 20, 2f)]      // want 40, only 20 is built
    [InlineData(20, 40, 0.5f)]    // want 20, only 40 is built
    [InlineData(30, 40, 0.75f)]
    public void ABorrowedHandleIsCorrectedToTheSizeAskedFor(int want, int have, float expected)
        => Assert.Equal(expected, FontManager.Correction(want, have), 3);

    [Fact]
    public void CorrectingAlwaysLandsOnTheRequestedSize()
    {
        // The property that matters: whatever we borrow, have * scale == want, so
        // text is never drawn at the wrong SIZE - only ever at the wrong sharpness.
        for (var want = 8; want <= 160; want += 2)
            for (var have = 8; have <= 160; have += 2)
            {
                var scale = FontManager.Correction(want, have);
                if (scale == 1f) continue;  // deliberately uncorrected, within 2%
                Assert.Equal(want, have * scale, 2);
            }
    }

    [Fact]
    public void TheUncorrectedCasesAreOnlyEverTheNearlyIdenticalOnes()
    {
        for (var want = 8; want <= 160; want += 2)
            for (var have = 8; have <= 160; have += 2)
                if (FontManager.Correction(want, have) == 1f)
                    Assert.True(MathF.Abs(want / (float)have - 1f) < 0.02f,
                        $"want {want} from {have} was left uncorrected but is {want / (float)have:0.000}x out");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-4)]
    public void AMissingSourceSizeNeverProducesAWildScale(int have)
        => Assert.Equal(1f, FontManager.Correction(40, have));

    [Fact]
    public void TheFamilyListStartsWithDefault()
    {
        // The dropdown's first entry is what a fresh config holds.
        Assert.Equal("Default", FontManager.FamilyNames[0]);
        Assert.Equal("Default", new Configuration().OverlayFontFamily);
    }
}
