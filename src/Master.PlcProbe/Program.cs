using System.Globalization;
using System.IO.Ports;
using Unitronics.ComDriver;
using Unitronics.ComDriver.Messages.DataRequest;

namespace Master_PlcProbe;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length is not (9 or 10))
        {
            Console.Error.WriteLine("ERROR|InvalidArguments|Se esperaban nueve parámetros PLC y, opcionalmente, direcciones MI.");
            return 1;
        }

        if (!TryParseSettings(args, out var settings, out var validationError))
        {
            Console.Error.WriteLine($"ERROR|InvalidSettings|{validationError}");
            return 1;
        }

        if (!TryParseAdditionalReadRequests(args, out var additionalReadRequests, out validationError))
        {
            Console.Error.WriteLine($"ERROR|InvalidSettings|{validationError}");
            return 1;
        }

        Serial? serial = null;
        PLC? plc = null;
        var exitCode = 1;
        var errorMessage = string.Empty;
        var connectionOutput = "CONNECTED";

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
                var requests = new ReadWriteRequest[additionalReadRequests.Length + 1];
                requests[0] = new ReadOperands(1, OperandTypes.MI, settings.AnalogInitial);
                for (var index = 0; index < additionalReadRequests.Length; index++)
                {
                    var readRequest = additionalReadRequests[index];
                    requests[index + 1] = new ReadOperands(1, readRequest.OperandType, readRequest.Address);
                }

                plc.ReadWrite(ref requests);

                if (requests[0].ResponseValues is not Array { Length: > 0 })
                {
                    errorMessage = $"ERROR|NoResponse|El PLC no devolvió una lectura de MI{settings.AnalogInitial}.";
                }
                else
                {
                    var values = new List<string>(additionalReadRequests.Length);
                    for (var index = 0; index < additionalReadRequests.Length; index++)
                    {
                        var readRequest = additionalReadRequests[index];
                        if (requests[index + 1].ResponseValues is not Array { Length: > 0 } response)
                        {
                            errorMessage =
                                $"ERROR|NoResponse|El PLC no devolvió una lectura de " +
                                $"{readRequest.OperandType}{readRequest.Address}.";
                            break;
                        }

                        var rawValue = response.GetValue(0);
                        if (rawValue is null)
                        {
                            errorMessage =
                                $"ERROR|NoResponse|El PLC devolvió un valor vacío para " +
                                $"{readRequest.OperandType}{readRequest.Address}.";
                            break;
                        }

                        var value = Convert.ToDouble(rawValue, CultureInfo.InvariantCulture);
                        values.Add(
                            $"{readRequest.OperandType}:{readRequest.Address}=" +
                            value.ToString("R", CultureInfo.InvariantCulture));
                    }

                    if (string.IsNullOrEmpty(errorMessage))
                    {
                        connectionOutput = values.Count == 0
                            ? "CONNECTED"
                            : $"CONNECTED|{string.Join("|", values)}";
                        exitCode = 0;
                    }
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
            Console.Out.WriteLine(connectionOutput);
        }
        else
        {
            Console.Error.WriteLine(errorMessage);
        }

        return exitCode;
    }

    private static bool TryParseAdditionalReadRequests(
        IReadOnlyList<string> args,
        out PlcReadRequest[] readRequests,
        out string error)
    {
        readRequests = Array.Empty<PlcReadRequest>();
        error = string.Empty;
        if (args.Count == 9)
        {
            return true;
        }

        var parsedRequests = new List<PlcReadRequest>();
        foreach (var requestText in args[9].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = requestText.Split(':');
            var operandType = OperandTypes.MI;
            var addressText = requestText;
            if (parts.Length == 2)
            {
                if (!Enum.TryParse(parts[0], ignoreCase: true, out operandType)
                    || !Enum.IsDefined(typeof(OperandTypes), operandType)
                    || operandType is not (OperandTypes.MI or OperandTypes.MB
                        or OperandTypes.MF or OperandTypes.ML or OperandTypes.Output))
                {
                    error = $"El tipo de operando '{parts[0]}' no es compatible con una lectura PLC.";
                    return false;
                }

                addressText = parts[1];
            }
            else if (parts.Length != 1)
            {
                error = $"La lectura adicional '{requestText}' debe tener el formato TIPO:DIRECCIÓN.";
                return false;
            }

            if (!ushort.TryParse(addressText, NumberStyles.None, CultureInfo.InvariantCulture, out var address))
            {
                error = $"La dirección adicional '{addressText}' no es válida.";
                return false;
            }

            var readRequest = new PlcReadRequest(operandType, address);
            if (!parsedRequests.Contains(readRequest))
            {
                parsedRequests.Add(readRequest);
            }
        }

        if (parsedRequests.Count == 0)
        {
            error = "Debe indicarse al menos una lectura adicional.";
            return false;
        }

        readRequests = parsedRequests.ToArray();
        return true;
    }

    private readonly struct PlcReadRequest : IEquatable<PlcReadRequest>
    {
        public PlcReadRequest(OperandTypes operandType, ushort address)
        {
            OperandType = operandType;
            Address = address;
        }

        public OperandTypes OperandType { get; }
        public ushort Address { get; }

        public bool Equals(PlcReadRequest other) =>
            OperandType == other.OperandType && Address == other.Address;

        public override bool Equals(object? obj) => obj is PlcReadRequest other && Equals(other);

        public override int GetHashCode() => ((int)OperandType * 397) ^ Address;
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
