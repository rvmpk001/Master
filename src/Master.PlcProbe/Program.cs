using System.Globalization;
using System.IO.Ports;
using Unitronics.ComDriver;
using Unitronics.ComDriver.Messages.DataRequest;

namespace Master_PlcProbe;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length != 9)
        {
            Console.Error.WriteLine("ERROR|InvalidArguments|Se esperaban nueve parámetros de comunicación PLC.");
            return 1;
        }

        if (!TryParseSettings(args, out var settings, out var validationError))
        {
            Console.Error.WriteLine($"ERROR|InvalidSettings|{validationError}");
            return 1;
        }

        Serial? serial = null;
        PLC? plc = null;
        var exitCode = 1;
        var errorMessage = string.Empty;

        try
        {
            PLCFactory.WorkWithXmlInternally = true;
            serial = new Serial(
                settings.PortName,
                settings.BaudRate,
                settings.Retry,
                settings.Timeout,
                settings.DataBits,
                settings.Parity,
                settings.StopBits,
                autoDetectComParams: false);
            plc = PLCFactory.GetPLC(serial, settings.PlcAddress, forceJazz: false);

            if (!plc.PLCChannel.Connected)
            {
                errorMessage = "ERROR|Disconnected|El canal serial no confirmó conexión con el PLC.";
            }
            else
            {
                var requests = new ReadWriteRequest[]
                {
                    new ReadOperands(1, OperandTypes.MI, settings.AnalogInitial)
                };
                plc.ReadWrite(ref requests);

                if (requests[0].ResponseValues is not Array { Length: > 0 })
                {
                    errorMessage = $"ERROR|NoResponse|El PLC no devolvió una lectura de MI{settings.AnalogInitial}.";
                }
                else
                {
                    exitCode = 0;
                }
            }
        }
        catch (ComDriveExceptions exception)
        {
            var errorCode = exception.ErrorCode.ToString();
            errorMessage = $"ERROR|{errorCode}|{exception.Message}";
            exitCode = exception.ErrorCode == ComDriveExceptions.ComDriveException.PortInUse ? 2 : 1;
        }
        catch (Exception exception)
        {
            errorMessage = $"ERROR|{exception.GetType().Name}|{exception.Message}";
        }
        finally
        {
            try
            {
                if (plc is not null)
                {
                    plc.Disconnect();
                }
                else
                {
                    serial?.Disconnect();
                }
            }
            catch (Exception exception)
            {
                exitCode = 1;
                errorMessage = $"ERROR|Disconnect|No se pudo liberar el puerto PLC: {exception.Message}";
            }
        }

        if (exitCode == 0)
        {
            Console.Out.WriteLine("CONNECTED");
        }
        else
        {
            Console.Error.WriteLine(errorMessage);
        }

        return exitCode;
    }

    private static bool TryParseSettings(
        IReadOnlyList<string> args,
        out PlcProbeSettings settings,
        out string error)
    {
        settings = null!;

        if (!int.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var plcAddress)
            || plcAddress is < 0 or > 127)
        {
            error = "Dirección PLC debe ser un número entre 0 y 127.";
            return false;
        }

        if (!Enum.TryParse(args[1], ignoreCase: true, out SerialPortNames portName)
            || !Enum.IsDefined(typeof(SerialPortNames), portName))
        {
            error = $"El puerto '{args[1]}' no es compatible con el driver.";
            return false;
        }

        if (!Enum.TryParse($"BR{args[2]}", ignoreCase: true, out BaudRate baudRate)
            || !Enum.IsDefined(typeof(BaudRate), baudRate))
        {
            error = $"La velocidad '{args[2]}' no está soportada.";
            return false;
        }

        if (!Enum.TryParse($"DB{args[3]}", ignoreCase: true, out DataBits dataBits)
            || !Enum.IsDefined(typeof(DataBits), dataBits))
        {
            error = $"DataBits '{args[3]}' no es compatible con el driver.";
            return false;
        }

        if (!Enum.TryParse(args[4], ignoreCase: true, out Parity parity)
            || !Enum.IsDefined(typeof(Parity), parity))
        {
            error = $"La paridad '{args[4]}' no es válida.";
            return false;
        }

        if (!Enum.TryParse(args[5], ignoreCase: true, out StopBits stopBits)
            || !Enum.IsDefined(typeof(StopBits), stopBits)
            || stopBits == StopBits.None)
        {
            error = $"StopBits '{args[5]}' no es compatible con el driver.";
            return false;
        }

        if (!int.TryParse(args[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out var timeout)
            || timeout <= 0)
        {
            error = "TimeOut debe ser mayor que cero.";
            return false;
        }

        if (!int.TryParse(args[7], NumberStyles.Integer, CultureInfo.InvariantCulture, out var retry)
            || retry < 0)
        {
            error = "Retry debe ser igual o mayor que cero.";
            return false;
        }

        if (!ushort.TryParse(args[8], NumberStyles.Integer, CultureInfo.InvariantCulture, out var analogInitial))
        {
            error = "AnalogInitial debe ser una dirección MI válida.";
            return false;
        }

        settings = new PlcProbeSettings(
            plcAddress,
            portName,
            baudRate,
            dataBits,
            parity,
            stopBits,
            timeout,
            retry,
            analogInitial);
        error = string.Empty;
        return true;
    }

    private sealed class PlcProbeSettings
    {
        public PlcProbeSettings(
            int plcAddress,
            SerialPortNames portName,
            BaudRate baudRate,
            DataBits dataBits,
            Parity parity,
            StopBits stopBits,
            int timeout,
            int retry,
            ushort analogInitial)
        {
            PlcAddress = plcAddress;
            PortName = portName;
            BaudRate = baudRate;
            DataBits = dataBits;
            Parity = parity;
            StopBits = stopBits;
            Timeout = timeout;
            Retry = retry;
            AnalogInitial = analogInitial;
        }

        public int PlcAddress { get; }
        public SerialPortNames PortName { get; }
        public BaudRate BaudRate { get; }
        public DataBits DataBits { get; }
        public Parity Parity { get; }
        public StopBits StopBits { get; }
        public int Timeout { get; }
        public int Retry { get; }
        public ushort AnalogInitial { get; }
    }
}
