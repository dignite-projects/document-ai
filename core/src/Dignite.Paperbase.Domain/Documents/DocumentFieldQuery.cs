using System;
using Dignite.Paperbase.Documents.Fields;

namespace Dignite.Paperbase.Documents;

/// <summary>
/// 已解析的单字段值查询——<see cref="IDocumentRepository.GetFieldMatchedIdsAsync"/> 的 <c>fieldQueries</c> 列表元素。
/// 同一次检索可带多个，仓储把各元素编译成对 <see cref="Document.ExtractedFieldValues"/> 的一个 <c>Any</c>（EXISTS）
/// 谓词、之间用 <c>AND</c> 拼接（结构化检索惯例：不同字段互相收窄）。
/// <para>
/// <see cref="FieldDefinitionId"/> 与 <see cref="FieldDataType"/> 由调用层（出口适配器）从 <c>FieldDefinition</c> 解析后填入
/// （#207：内部按不可变 Id 匹配 child 行，不再按字段名字符串）——仓储据此用 <see cref="FieldDefinitionId"/> 定位 child、
/// 再分派到对应类型化列做普通等值 / 区间比较；仓储不依赖其它聚合的仓储。<see cref="FieldName"/> 仅用于错误信息
/// （可读诊断），不参与匹配。
/// </para>
/// 等值（<see cref="FieldValue"/>）与区间（<see cref="FieldValueMin"/> / <see cref="FieldValueMax"/>）
/// 至少给其一，否则该查询残缺 → 仓储 fail-closed 空结果，绝不退化成"该类型全捞"。
/// </summary>
public sealed record DocumentFieldQuery(
    Guid FieldDefinitionId,
    string FieldName,
    FieldDataType FieldDataType,
    string? FieldValue = null,
    string? FieldValueMin = null,
    string? FieldValueMax = null);
