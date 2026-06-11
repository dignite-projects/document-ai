using Volo.Abp.Reflection;

namespace Dignite.DocumentAI.Permissions;

public class DocumentAIPermissions
{
    public const string GroupName = "DocumentAI";

    public static class Documents
    {
        public const string Default = GroupName + ".Documents";
        public const string Upload = Default + ".Upload";
        public const string Delete = Default + ".Delete";
        public const string PermanentDelete = Default + ".PermanentDelete";
        public const string Restore = Default + ".Restore";
        public const string Export = Default + ".Export";
        public const string ConfirmClassification = Default + ".ConfirmClassification";

        public static class Pipelines
        {
            public const string Default = Documents.Default + ".Pipelines";
            public const string Retry = Default + ".Retry";
        }

        // 存量文档批量重处理（#289）——admin 级操作：配置（分类提示词 / 字段定义）调整后对存量重跑。
        // 单篇「仅重抽字段」走 ConfirmClassification（操作员级，与「重新识别」对称）；批量入口走这里。
        public static class Reprocessing
        {
            public const string Default = Documents.Default + ".Reprocessing";

            /// <summary>批量字段重抽（叶子操作，轻警告）。</summary>
            public const string FieldExtraction = Default + ".FieldExtraction";

            /// <summary>批量重新分类（级联 + 破坏性，重警告）。</summary>
            public const string Reclassification = Default + ".Reclassification";
        }

        public static class Templates
        {
            public const string Default = Documents.Default + ".Templates";
            public const string Create = Default + ".Create";
            public const string Update = Default + ".Update";
            public const string Delete = Default + ".Delete";
        }
    }

    // 文件柜（#194）——人工组织维度，与 Documents 同级权限组。
    public static class Cabinets
    {
        public const string Default = GroupName + ".Cabinets";
        public const string Create = Default + ".Create";
        public const string Update = Default + ".Update";
        public const string Delete = Default + ".Delete";
    }

    // 文档类型 schema 管理（#217）——admin 级操作，独立于文档 CRUD。
    public static class DocumentTypes
    {
        public const string Default = GroupName + ".DocumentTypes";
        public const string Create = Default + ".Create";
        public const string Update = Default + ".Update";
        public const string Delete = Default + ".Delete";
    }

    // 字段定义 schema 管理（#217）——admin 级操作，独立于文档 CRUD。
    public static class FieldDefinitions
    {
        public const string Default = GroupName + ".FieldDefinitions";
        public const string Create = Default + ".Create";
        public const string Update = Default + ".Update";
        public const string Delete = Default + ".Delete";
    }

    public static string[] GetAll()
    {
        return ReflectionHelper.GetPublicConstantsRecursively(typeof(DocumentAIPermissions));
    }
}
