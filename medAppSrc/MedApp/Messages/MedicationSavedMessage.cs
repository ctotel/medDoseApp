using CommunityToolkit.Mvvm.Messaging.Messages;

namespace MedApp.Messages;

/// <summary>
/// Sent after a medication is added or edited so that list pages can refresh.
/// </summary>
public class MedicationSavedMessage : ValueChangedMessage<int>
{
    public MedicationSavedMessage(int medicationId) : base(medicationId) { }
}
