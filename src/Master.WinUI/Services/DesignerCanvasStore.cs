using System.Text.Json;
using Windows.Storage;

namespace Master_WinUI.Services;

public sealed class DesignerCanvasStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    private readonly string _filePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Master.WinUI",
        "designer-canvas.json");

    public async Task<DesignerCanvasState> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return new DesignerCanvasState();
        }

        await using var stream = File.OpenRead(_filePath);
        return await JsonSerializer.DeserializeAsync<DesignerCanvasState>(stream, SerializerOptions, cancellationToken)
            ?? throw new InvalidDataException("El archivo guardado del lienzo está vacío o no contiene una configuración válida.");
    }

    public async Task SaveAsync(DesignerCanvasState state, CancellationToken cancellationToken = default)
    {
        var snapshot = new DesignerCanvasState
        {
            IsLocked = state.IsLocked,
            ImagePath = state.ImagePath,
            Controls = state.Controls.Select(control => new DesignerCanvasControlState
            {
                Id = control.Id,
                Type = control.Type,
                Text = control.Text,
                Left = control.Left,
                Top = control.Top,
                IsOn = control.IsOn
            }).ToList()
        };
        var directory = Path.GetDirectoryName(_filePath)
            ?? throw new InvalidOperationException("No se pudo determinar la carpeta para guardar el lienzo.");
        Directory.CreateDirectory(directory);

        var temporaryPath = $"{_filePath}.tmp";
        await _saveLock.WaitAsync(cancellationToken);
        try
        {
            await using (var stream = File.Create(temporaryPath))
            {
                await JsonSerializer.SerializeAsync(stream, snapshot, SerializerOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, _filePath, overwrite: true);
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
            finally
            {
                _saveLock.Release();
            }
        }
    }

    public async Task<string> CopyImageToStoreAsync(StorageFile imageFile, CancellationToken cancellationToken = default)
    {
        var extension = imageFile.FileType.ToLowerInvariant();
        if (extension is not (".png" or ".jpg" or ".jpeg" or ".bmp"))
        {
            throw new InvalidDataException("El formato de imagen no es compatible.");
        }

        var imageDirectory = Path.Combine(Path.GetDirectoryName(_filePath)!, "CanvasImages");
        Directory.CreateDirectory(imageDirectory);
        var storedPath = Path.Combine(imageDirectory, $"template{extension}");
        if (string.Equals(Path.GetFullPath(imageFile.Path), storedPath, StringComparison.OrdinalIgnoreCase))
        {
            return storedPath;
        }

        var temporaryPath = $"{storedPath}.tmp";
        try
        {
            await using (var source = await imageFile.OpenStreamForReadAsync())
            await using (var destination = File.Create(temporaryPath))
            {
                await source.CopyToAsync(destination, cancellationToken);
                await destination.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, storedPath, overwrite: true);
            return storedPath;
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}

public sealed class DesignerCanvasState
{
    public bool IsLocked { get; set; }
    public string? ImagePath { get; set; }
    public List<DesignerCanvasControlState> Controls { get; set; } = new();
}

public sealed class DesignerCanvasControlState
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Type { get; set; } = "Etiqueta";
    public string Text { get; set; } = "Control";
    public double Left { get; set; }
    public double Top { get; set; }
    public bool IsOn { get; set; }
}
