using App.Core.Interfaces;
using App.Core.Models;
using WinFormsApp.ViewModels;

namespace WinFormsApp.Controllers;

internal sealed class DeviceManagementController
{
    private readonly IManagedDeviceService _deviceService;

    public DeviceManagementController(IManagedDeviceService deviceService)
    {
        _deviceService = deviceService;
    }

    public DeviceManagementDashboardViewModel Load(DeviceFilterViewModel filter)
    {
        var result = _deviceService.Query(new ManagedDeviceQuery(
            filter.Keyword,
            filter.LineName,
            filter.Status));

        return new DeviceManagementDashboardViewModel
        {
            Devices = result.Devices.Select(ToRow).ToList(),
            LineOptions = result.LineOptions,
            TotalCount = result.Overview.TotalCount,
            ActiveCount = result.Overview.ActiveCount,
            MaintenanceCount = result.Overview.MaintenanceCount,
            StoppedCount = result.Overview.StoppedCount,
            CommunicationLinkedCount = result.Overview.CommunicationLinkedCount,
            GeneratedAt = result.GeneratedAt
        };
    }

    public DeviceRowViewModel Save(DeviceEditorViewModel editor)
    {
        var device = _deviceService.Save(new ManagedDeviceDraft(
            editor.LineName,
            editor.DeviceName,
            editor.DeviceCode,
            editor.Location,
            editor.Owner,
            editor.CommunicationAddress,
            editor.Status,
            editor.Remark,
            editor.Id));

        return ToRow(device);
    }

    public void Delete(Guid id)
    {
        _deviceService.Delete(id);
    }

    public int EnsureDevicesFromInspection(IEnumerable<InspectionEntryViewModel> entries)
    {
        var dashboard = Load(new DeviceFilterViewModel());
        var existingKeys = dashboard.Devices
            .Select(device => BuildDeviceKey(device.LineName, device.DeviceName))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var createdCount = 0;

        foreach (var entry in entries
                     .Where(entry => !string.IsNullOrWhiteSpace(entry.LineName) && !string.IsNullOrWhiteSpace(entry.DeviceName))
                     .GroupBy(entry => BuildDeviceKey(entry.LineName, entry.DeviceName), StringComparer.OrdinalIgnoreCase)
                     .Select(group => group.First()))
        {
            if (!existingKeys.Add(BuildDeviceKey(entry.LineName, entry.DeviceName)))
            {
                continue;
            }

            Save(new DeviceEditorViewModel
            {
                LineName = entry.LineName,
                DeviceName = entry.DeviceName,
                Location = entry.LineName,
                Owner = entry.Inspector,
                Status = ManagedDeviceStatus.Active,
                Remark = "由巡检记录自动加入台账"
            });
            createdCount++;
        }

        return createdCount;
    }

    private static DeviceRowViewModel ToRow(ManagedDevice device)
    {
        return new DeviceRowViewModel
        {
            Id = device.Id,
            DeviceCode = device.DeviceCode,
            LineName = device.LineName,
            DeviceName = device.DeviceName,
            Location = device.Location,
            Owner = device.Owner,
            CommunicationAddress = device.CommunicationAddress,
            Status = device.Status,
            StatusText = device.Status.ToDisplayText(),
            UpdatedAtText = device.UpdatedAt.ToString("MM-dd HH:mm"),
            Remark = device.Remark
        };
    }

    private static string BuildDeviceKey(string lineName, string deviceName)
    {
        return $"{lineName.Trim()}|{deviceName.Trim()}".ToUpperInvariant();
    }
}
