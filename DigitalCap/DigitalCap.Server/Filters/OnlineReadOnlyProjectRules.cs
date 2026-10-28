using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DigitalCap.WebApi.Filters
{
    /// <summary>
    /// Pure decision logic of <see cref="OnlineReadOnlyProjectFilter"/> (no ASP.NET types, so it can be unit tested):
    /// which requests are edits and which project they edit.
    /// </summary>
    public static class OnlineReadOnlyProjectRules
    {
        public const string ConflictMessage = "This project is downloaded on this computer. Edit it offline, then sync.";

        private static readonly HashSet<string> MutatingMethods = new(StringComparer.OrdinalIgnoreCase)
        {
            "POST", "PUT", "PATCH", "DELETE"
        };

        /// <summary>Controllers that manage the local copy, sign-in and diagnostics — never project edits.</summary>
        private static readonly HashSet<string> ExemptControllers = new(StringComparer.OrdinalIgnoreCase)
        {
            "DataTransferAPI", "Offline", "Auth", "Diag"
        };

        /// <summary>
        /// POST actions that only read or export (grids, validation, PDF/export plumbing) are allowed. Prefixes cover
        /// GetAllProjectsGridData, GetSummary, GetReportBySectionId, GetReportTemplatePart, GetClientAdminsAsync,
        /// Validate*, Check*, Export* and Download*.
        /// </summary>
        private static readonly string[] ReadOnlyActionPrefixes = { "Get", "Validate", "Check", "Export", "Download" };

        /// <summary>
        /// Explicit read-only POSTs that do not match a prefix: report export plumbing (SurveyReport/Survey
        /// SaveReportFile, MergeReportPDF/MergeReportPdf, SurveyReportPDF) and ProjectController.ReleaseOfflineDownload
        /// (an administrator releasing the download lock, not a project edit). Other Save*/Merge* actions stay blocked.
        /// </summary>
        private static readonly HashSet<string> ReadOnlyActions = new(StringComparer.OrdinalIgnoreCase)
        {
            "SaveReportFile", "MergeReportPDF", "SurveyReportPDF", "ReleaseOfflineDownload"
        };

        private static readonly string[] ProjectIdNames = { "projectId", "projectID" };

        /// <summary>True when the request may change data and is subject to the read-only rule.</summary>
        public static bool IsGuardedRequest(string? httpMethod, string? controller, string? action)
        {
            if (string.IsNullOrEmpty(httpMethod) || !MutatingMethods.Contains(httpMethod))
                return false;
            if (!string.IsNullOrEmpty(controller) && ExemptControllers.Contains(controller))
                return false;
            if (string.IsNullOrEmpty(action))
                return true;
            if (ReadOnlyActions.Contains(action))
                return false;
            return !ReadOnlyActionPrefixes.Any(p => action.StartsWith(p, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Finds the project an action edits: an int argument named projectId/ProjectId, or a public int/int?
        /// ProjectId/ProjectID property of a bound argument. ProjectController also identifies the project as
        /// "id" (Update, CloseProject, DeleteProject, CancelProject, UpdatePriority) or as the Id of a bound
        /// <c>Project</c> model (Update_Project, Create has Id 0 and is ignored).
        /// </summary>
        public static bool TryGetProjectId(string? controller, IDictionary<string, object?> arguments, out int projectId)
        {
            projectId = 0;
            if (arguments == null || arguments.Count == 0)
                return false;

            // 1. Direct argument.
            foreach (var (name, value) in arguments)
            {
                if (ProjectIdNames.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase)) && TryPositiveInt(value, out projectId))
                    return true;
            }

            // 2. Property of a bound object.
            foreach (var value in arguments.Values)
            {
                if (value == null || IsSimple(value.GetType()))
                    continue;
                var prop = value.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(p => p.GetIndexParameters().Length == 0
                                         && ProjectIdNames.Any(n => string.Equals(n, p.Name, StringComparison.OrdinalIgnoreCase))
                                         && (p.PropertyType == typeof(int) || p.PropertyType == typeof(int?)));
                if (prop != null && TryPositiveInt(prop.GetValue(value), out projectId))
                    return true;
            }

            // 3. ProjectController: "id" is the project id, and a bound Project model's Id.
            if (string.Equals(controller, "Project", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var (name, value) in arguments)
                {
                    if (string.Equals(name, "id", StringComparison.OrdinalIgnoreCase) && TryPositiveInt(value, out projectId))
                        return true;
                }
                foreach (var value in arguments.Values)
                {
                    if (value == null || value.GetType().Name != "Project")
                        continue;
                    var idProp = value.GetType().GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
                    if (idProp != null && (idProp.PropertyType == typeof(int) || idProp.PropertyType == typeof(int?))
                        && TryPositiveInt(idProp.GetValue(value), out projectId))
                        return true;
                }
            }

            projectId = 0;
            return false;
        }

        private static bool TryPositiveInt(object? value, out int result)
        {
            result = value is int i ? i : 0;
            return result > 0;
        }

        private static bool IsSimple(Type type)
            => type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal)
               || type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(Guid)
               || Nullable.GetUnderlyingType(type) != null;
    }
}
