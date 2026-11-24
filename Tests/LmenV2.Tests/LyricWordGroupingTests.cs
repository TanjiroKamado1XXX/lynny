using System.Windows;
using System.Windows.Media;
using LmenV2.Models;
using LmenV2.Rendering.Kinetic;
using LmenV2.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LmenV2.Tests;

[TestClass]
public sealed class LyricWordGroupingTests
{
    [TestMethod]
    public void TtmlWhitespaceSeparatesWordsButAdjacentSpansJoinSyllables()
    {
        const string ttml =
            "<tt><body><p begin=\"0s\" end=\"2s\">" +
            "<span begin=\"0s\" end=\"0.2s\">I</span> " +
            "<span begin=\"0.2s\" end=\"0.5s\">DID</span>" +
            "<span begin=\"0.5s\" end=\"0.8s\">N'T</span> " +
            "<span begin=\"0.8s\" end=\"1s\">CRY</span>" +
            "</p></body></tt>";
        var line = TtmlLyrics.Parse(ttml).Single();

        AssertWords(line, "I", "DIDN'T", "CRY");
        Assert.AreEqual(2, line.Words[1].Syllables.Count);
        Assert.AreEqual("I DIDN'T CRY", line.Text);
    }

    [TestMethod]
    public void AdjacentTtmlAndKaraokeSyllablesStayInOneWord()
    {
        const string ttml =
            "<tt><body><p begin=\"0s\" end=\"1s\">" +
            "<span begin=\"0s\" end=\"0.2s\">FO</span>" +
            "<span begin=\"0.2s\" end=\"0.4s\">RE</span>" +
            "<span begin=\"0.4s\" end=\"1s\">VER?</span>" +
            "</p></body></tt>";
        var ttmlLine = TtmlLyrics.Parse(ttml).Single();

        var karaokeLine = KaraokeLrc.Parse(
            "[0,1000](0,200)FO(200,200)RE(400,600)VER?").Single();

        AssertSingleSyllableGroupedWord(ttmlLine.Words.Single());
        AssertSingleSyllableGroupedWord(karaokeLine.Words.Single());
        Assert.AreEqual("FOREVER?", ttmlLine.Text);
        Assert.AreEqual("FOREVER?", karaokeLine.Text);
    }

    [TestMethod]
    public void KaraokeLineKeepsInterWordSpacesAndJoinsOnlySyllables()
    {
        var line = KaraokeLrc.Parse(
            "[0,1500](0,200)AND (200,200)CAN (400,100)YOU (500,150)MAKE (650,100)IT (750,150)LAST (900,100)FO(1000,100)RE(1100,300)VER?").Single();

        AssertWords(line, "AND", "CAN", "YOU", "MAKE", "IT", "LAST", "FOREVER?");
        Assert.AreEqual(3, line.Words[^1].Syllables.Count);
    }

    [TestMethod]
    public void EnhancedLrcTreatsEachTimedTokenAsAWordWithoutTrailingWhitespace()
    {
        var line = EnhancedLrc.Parse(
            "[00:00.00]<00:01.00>AND<00:01.20>I'M<00:01.40>STILL").Single();

        AssertWords(line, "AND", "I'M", "STILL");
        Assert.AreEqual("AND I'M STILL", line.Text);
    }

    [TestMethod]
    public void ProviderSourceTextRestoresOmittedTokenSeparators()
    {
        var runs = new List<TimedRun>
        {
            new(0, 100, "AND"),
            new(100, 200, "I'M"),
            new(200, 300, "STILL")
        };
        var words = SyllableRuns.WordsFromRuns(runs, sourceText: "AND I'M STILL");
        var line = new LyricLine { Words = words, Text = LyricLine.ComposeText(words) };

        AssertWords(line, "AND", "I'M", "STILL");
    }

    [TestMethod]
    public void KaraokeParserPreservesParentheticalAdLibsAndContractions()
    {
        var line = KaraokeLrc.Parse(
            "[0,500](0,250)GLOWIN' (250,250)(YEAH)").Single();

        AssertWords(line, "GLOWIN'", "(YEAH)");
        Assert.AreEqual("GLOWIN' (YEAH)", line.Text);
    }

    [TestMethod]
    public void WordLevelLrcAndPlainLyricsKeepTheirSpaces()
    {
        var wordLevel = LrcLibParser.ParseLrc(
            "[00:00.00]<00:00.00>AND <00:00.20>I'M").Single();
        var plain = LrcLibParser.ParseLrc(
            "[00:00.00]AND I'M STILL UP GLOWIN' (YEAH)").Single();

        Assert.AreEqual("AND I'M", wordLevel.Text);
        Assert.AreEqual("AND I'M STILL UP GLOWIN' (YEAH)", plain.Text);
    }

    [TestMethod]
    public void KineticRowsMeasureOnlyInterWordSpaceAndNeverSplitSyllables()
    {
        var font = new FontFamily("Segoe UI, Inter, Arial Black");
        double wordSpaceWidth = KineticLayout.WordSpaceWidth(font, FontWeights.Black, 1.0);
        var forever = new LyricWord
        {
            Text = "FOREVER?",
            Syllables =
            {
                new LyricSyllable(0, 100, 0, 2),
                new LyricSyllable(100, 200, 2, 4),
                new LyricSyllable(200, 300, 4, 8)
            }
        };
        var and = new LyricWord { Text = "AND", HasSpaceAfter = true };
        var can = new LyricWord { Text = "CAN" };

        Assert.IsTrue(wordSpaceWidth > 0.0);
        Assert.AreEqual(
            KineticLayout.VisualLength("FOREVER?"),
            KineticLayout.RowVisualLength(new[] { forever }, wordSpaceWidth),
            0.0001);
        Assert.AreEqual(
            KineticLayout.VisualLength("AND") + wordSpaceWidth + KineticLayout.VisualLength("CAN"),
            KineticLayout.RowVisualLength(new[] { and, can }, wordSpaceWidth),
            0.0001);

        var words = new List<LyricWord> { forever, and, can };
        var rows = KineticLayout.GroupRows(words, 3);
        CollectionAssert.AreEqual(words, rows.SelectMany(row => row).ToList());
        Assert.AreSame(forever, rows[0][0]);
    }

    private static void AssertSingleSyllableGroupedWord(LyricWord word)
    {
        Assert.AreEqual("FOREVER?", word.Text);
        Assert.AreEqual(3, word.Syllables.Count);
        Assert.AreEqual(0, word.Syllables[0].StartChar);
        Assert.AreEqual(2, word.Syllables[0].EndChar);
        Assert.AreEqual(2, word.Syllables[1].StartChar);
        Assert.AreEqual(4, word.Syllables[1].EndChar);
        Assert.AreEqual(4, word.Syllables[2].StartChar);
        Assert.AreEqual(8, word.Syllables[2].EndChar);
    }

    private static void AssertWords(LyricLine line, params string[] expected)
    {
        CollectionAssert.AreEqual(expected, line.Words.Select(word => word.Text).ToArray());
        Assert.AreEqual(string.Join(" ", expected), line.Text);
        Assert.AreEqual(string.Join(" ", expected), string.Join(" ", line.Words.Select(word => word.Text)));
        Assert.AreEqual(string.Join(" ", expected), LyricLine.ComposeText(line.Words));
        for (int i = 0; i < line.Words.Count - 1; i++)
        {
            Assert.IsTrue(line.Words[i].HasSpaceAfter, $"Missing separator after {line.Words[i].Text}.");
        }
    }
}
