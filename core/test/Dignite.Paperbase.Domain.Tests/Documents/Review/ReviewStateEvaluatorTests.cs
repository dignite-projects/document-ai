using System;
using Dignite.Paperbase.Documents.Review;
using Shouldly;
using Xunit;

namespace Dignite.Paperbase.Documents;

/// <summary>
/// #284：审核判定纯函数单测——<see cref="ReviewStateEvaluator"/>（必填缺失维度）+
/// <see cref="ReviewReasonPolicy"/>（阻断性）。不依赖 DB / DI，直接构造。
/// </summary>
public class ReviewStateEvaluatorTests
{
    private readonly ReviewStateEvaluator _evaluator = new();

    [Fact]
    public void MissingRequiredFields_Empty_Required_Returns_False()
    {
        _evaluator.MissingRequiredFieldsPresent(Array.Empty<Guid>(), new[] { Guid.NewGuid() })
            .ShouldBeFalse();
    }

    [Fact]
    public void MissingRequiredFields_All_Present_Returns_False()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        _evaluator.MissingRequiredFieldsPresent(new[] { a, b }, new[] { a, b, Guid.NewGuid() })
            .ShouldBeFalse();
    }

    [Fact]
    public void MissingRequiredFields_Some_Missing_Returns_True()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        _evaluator.MissingRequiredFieldsPresent(new[] { a, b }, new[] { a })
            .ShouldBeTrue();
    }

    [Fact]
    public void MissingRequiredFields_Nothing_Extracted_Returns_True()
    {
        _evaluator.MissingRequiredFieldsPresent(new[] { Guid.NewGuid() }, Array.Empty<Guid>())
            .ShouldBeTrue();
    }

    // 只有 UnresolvedClassification 是 blocking（阻断 Ready）；MissingRequiredFields 是 non-blocking。
    [Theory]
    [InlineData(DocumentReviewReasons.None, false)]
    [InlineData(DocumentReviewReasons.UnresolvedClassification, true)]
    [InlineData(DocumentReviewReasons.MissingRequiredFields, false)]
    [InlineData(DocumentReviewReasons.UnresolvedClassification | DocumentReviewReasons.MissingRequiredFields, true)]
    public void HasBlocking_Only_UnresolvedClassification_Is_Blocking(DocumentReviewReasons reasons, bool expected)
    {
        ReviewReasonPolicy.HasBlocking(reasons).ShouldBe(expected);
    }

    // 任一未解决原因都需操作员关注（进队列）。
    [Theory]
    [InlineData(DocumentReviewReasons.None, false)]
    [InlineData(DocumentReviewReasons.MissingRequiredFields, true)]
    [InlineData(DocumentReviewReasons.UnresolvedClassification, true)]
    public void RequiresAttention_True_When_Any_Reason(DocumentReviewReasons reasons, bool expected)
    {
        ReviewReasonPolicy.RequiresAttention(reasons).ShouldBe(expected);
    }
}
