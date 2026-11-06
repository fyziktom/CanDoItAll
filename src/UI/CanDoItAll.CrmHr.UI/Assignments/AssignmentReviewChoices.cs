using CanDoItAll.Editing;

namespace CanDoItAll.CrmHr.UI.Assignments;

internal static class AssignmentReviewChoices {
    internal static void Keep<T>(MergeField<T> previous, MergeField<T> current) {
        var equality = EqualityComparer<T>.Default;
        if (!equality.Equals(previous.Original, current.Original) || !equality.Equals(previous.Local, current.Local) ||
            !equality.Equals(previous.Current, current.Current)) {
            return;
        }
        switch (previous.Choice) {
            case MergeChoice.Local:
                current.UseLocal();
                break;
            case MergeChoice.Current:
                current.UseCurrent();
                break;
            case MergeChoice.Custom:
                current.SetResult(previous.Result);
                break;
        }
    }
}
