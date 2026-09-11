namespace DoradoCloud.Modules.Legacy.Resources;

/// <summary>
/// Built-in firmware catalog used when no <c>Resources:Devices</c> are
/// configured. It describes the known Zune device families (a factual
/// interoperability catalog); the update text is authored by Dorado and the
/// baseline CABs are supplied by the operator's external corpus.
/// </summary>
public static class FirmwareCatalogDefaults
{
    public const string Manufacturer = "Dorado Cloud";
    public const string Name = "Zune";
    public const string Title = "Zune firmware update";
    public const string Description =
        "Firmware update served by Dorado Cloud from a user-supplied local corpus. "
        + "Dorado is an independent, non-affiliated homage and ships no Microsoft firmware.";
    public const string Eula =
        "Provided by the Dorado community for personal use with hardware you own. "
        + "Zune is a trademark of Microsoft Corporation.";

    public static List<FirmwareDeviceOptions> Create() => new()
    {
        new FirmwareDeviceOptions
        {
            DeviceClass = 1,
            FamilyId = 0,
            HardwareId = @"USB\Vid_045e&Pid_0710&Rev_0100",
            Version = "03.30.00039.00-00435",
            FileName = "KeelBaseline.cab",
            UpgradeFrom =
            {
                "00.00.00000.00-00.00",
                "01.04.00485.00-00425",
                "02.05.01614.00-00435",
                "03.20.00035.00-00435",
            },
        },
        new FirmwareDeviceOptions
        {
            DeviceClass = 1,
            FamilyId = 2,
            HardwareId = @"USB\Vid_045e&Pid_0710&Rev_0200",
            Version = "03.30.00039.00-01620",
            FileName = "ScorpiusBaseline.cab",
            UpgradeFrom =
            {
                "00.00.00000.00-00000",
                "02.05.01614.00-01613",
                "03.20.00035.00-01620",
            },
        },
        new FirmwareDeviceOptions
        {
            DeviceClass = 1,
            FamilyId = 3,
            HardwareId = @"USB\Vid_045e&Pid_0710&Rev_0300",
            Version = "03.30.00039.00-01620",
            FileName = "DracoBaseline.cab",
            UpgradeFrom =
            {
                "00.00.00000.00-00000",
                "02.05.01614.00-01613",
                "03.20.00035.00-01620",
            },
        },
        new FirmwareDeviceOptions
        {
            DeviceClass = 2,
            FamilyId = 6,
            HardwareId = @"USB\Vid_045e&Pid_063E&Rev_0100",
            Version = "04.05.00114.00-04.05.00114.00-04.05.00114.00",
            FileName = "PavoBaseline.cab",
            UpgradeFrom =
            {
                "00.00.00000.00-00.00.00000.00-00.00.00000.00",
                "04.03.00191.00-04.03.00191.00-04.03.00191.00",
                "04.05.00109.00-04.05.00109.00-04.05.00109.00",
            },
        },
    };
}
