using MedApp.ViewModels;

namespace MedApp.Views;

public partial class AddEditMedicationPage : ContentPage
{
    private readonly AddEditMedicationViewModel _vm;

    public AddEditMedicationPage()
    {
        InitializeComponent();
        _vm = Handler?.MauiContext?.Services.GetRequiredService<AddEditMedicationViewModel>()
            ?? IPlatformApplication.Current!.Services.GetRequiredService<AddEditMedicationViewModel>();
        BindingContext = _vm;
    }

    public AddEditMedicationPage(AddEditMedicationViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    /// <summary>
    /// TimePicker doesn't support two-way binding to a collection item,
    /// so we forward changes to the VM manually.
    /// </summary>
    private void OnTimePickerPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(TimePicker.Time)) return;
        if (sender is not TimePicker picker) return;

        var index = GetIndexOfPicker(picker);
        if (index >= 0)
            _vm.UpdateTime(index, picker.Time);
    }

    private void OnRemoveTimeClicked(object? sender, EventArgs e)
    {
        if (sender is not ImageButton btn) return;

        var parent = btn.Parent as Grid;
        var index = GetIndexOfElement(parent);
        if (index >= 0)
            _vm.RemoveTimeCommand.Execute(index);
    }

    private int GetIndexOfPicker(TimePicker picker)
    {
        var parent = picker.Parent; // Grid
        return GetIndexOfElement(parent);
    }

    private int GetIndexOfElement(Element? element)
    {
        if (element?.Parent is not Layout layout) return -1;

        for (var i = 0; i < layout.Children.Count; i++)
        {
            if (ReferenceEquals(layout.Children[i], element))
                return i;
        }
        return -1;
    }
}
