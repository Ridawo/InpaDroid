using Android.App;
using Android.Content;
using Android.Hardware.Usb;
using EdiabasLib;

namespace InpaDroid.Ui;

/// <summary>
/// Android no deja abrir el cable FTDI (K+DCAN) sin permiso del usuario: lo pedimos al volver a cada pantalla.
/// </summary>
public static class UsbPermission
{
    const string Action = "com.inpadroid.app.USB_PERMISSION";
    static readonly HashSet<int> Requested = new();

    /// <param name="force">true al pulsar "Detectar cable": vuelve a preguntar aunque el usuario ya lo denegara.</param>
    public static void RequestIfNeeded(Context ctx, bool force = false)
    {
        try
        {
            if (ctx.GetSystemService(Context.UsbService) is not UsbManager usb)
                return;
            foreach (var driver in EdFtdiInterface.GetDriverList(usb))
            {
                var device = driver.Device;
                if (usb.HasPermission(device) || (!Requested.Add(device.DeviceId) && !force))
                    continue;
                // Android 12+ exige indicar la mutabilidad; Android 14+ exige intent explícito si es mutable.
                var flags = OperatingSystem.IsAndroidVersionAtLeast(31) ? PendingIntentFlags.Mutable : 0;
                var intent = new Intent(Action).SetPackage(ctx.PackageName);
                usb.RequestPermission(device, PendingIntent.GetBroadcast(ctx, 0, intent, flags));
            }
        }
        catch (Exception)
        {
            // Algunos fabricantes lanzan excepción aquí; la conexión fallará después con su propio mensaje.
        }
    }
}
