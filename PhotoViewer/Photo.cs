using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PhotoViewer;

// цвет пикселя в формате RGB
public struct Pixel
{
    public byte R { get; set; }
    public byte G { get; set; }
    public byte B { get; set; }

    public Pixel(byte r, byte g, byte b)
    {
        R = r;
        G = g;
        B = b;
    }

    public static Pixel Black => new Pixel(0, 0, 0);
    public static Pixel White => new Pixel(255, 255, 255);

    public static Pixel FromRgb(int r, int g, int b)
    {
        byte cr = (byte)Math.Clamp(r, 0, 255);
        byte cg = (byte)Math.Clamp(g, 0, 255);
        byte cb = (byte)Math.Clamp(b, 0, 255);
        return new Pixel(cr, cg, cb);
    }

    public override string ToString() => $"RGB({R}, {G}, {B})";
}

// изменённый пиксель с координатами
public class PixelPoint
{
    public int X { get; set; }
    public int Y { get; set; }
    public Pixel Color { get; set; }

    public PixelPoint() { }

    public PixelPoint(int x, int y, Pixel color)
    {
        X = x;
        Y = y;
        Color = color;
    }
}

// текстовая надпись на фотографии
public class TextLabel
{
    public string Text { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public Pixel Color { get; set; }

    public TextLabel() { }

    public TextLabel(string text, int x, int y, Pixel color)
    {
        Text = text;
        X = x;
        Y = y;
        Color = color;
    }
}

// элемент коллажа
public class CollageItem
{
    public string PhotoPath { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }

    public CollageItem() { }

    public CollageItem(string photoPath, int x, int y, int width, int height)
    {
        PhotoPath = photoPath;
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }
}

// модель фотографии
public class Photo
{
    public static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public string FilePath { get; set; } = "";
    public int Width { get; set; }
    public int Height { get; set; }
    public Pixel BaseColor { get; set; } = Pixel.Black;

    public List<PixelPoint> ModifiedPixels { get; set; } = new List<PixelPoint>();
    public List<TextLabel> Labels { get; set; } = new List<TextLabel>();
    public List<CollageItem> CollageItems { get; set; } = new List<CollageItem>();
    public List<string> Changes { get; set; } = new List<string>();
    public List<Photo> History { get; set; } = new List<Photo>();

    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp" };

    public static void ValidateFilePath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("Путь к файлу не может быть пустым.");

        string ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (Array.IndexOf(AllowedExtensions, ext) < 0)
            throw new ArgumentException("Путь должен указывать на файл изображения (.jpg, .jpeg, .png, .bmp, .gif, .webp).");
    }

    // открывает существующий файл изображения с диска
    public static Photo Open(string filePath)
    {
        ValidateFilePath(filePath);
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Файл '{filePath}' не найден.");

        return new Photo(filePath);
    }

    public Photo() { }

    public Photo(string filePath, int width = 800, int height = 600, Pixel? fill = null)
    {
        ValidateFilePath(filePath);
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Размеры должны быть положительными.");

        FilePath = filePath;
        Width = width;
        Height = height;
        BaseColor = fill ?? Pixel.Black;
    }

    public bool IsInBounds(int x, int y)
    {
        return x >= 0 && x < Width && y >= 0 && y < Height;
    }

    public void EnsureInBounds(int x, int y)
    {
        if (!IsInBounds(x, y))
            throw new ArgumentOutOfRangeException($"Координаты ({x}, {y}) вне границ {Width}x{Height}.");
    }

    // доступ к отдельному пикселю по координатам (x, y)
    [JsonIgnore]
    public Pixel this[int x, int y]
    {
        get
        {
            EnsureInBounds(x, y);
            for (int i = ModifiedPixels.Count - 1; i >= 0; i--)
            {
                if (ModifiedPixels[i].X == x && ModifiedPixels[i].Y == y)
                    return ModifiedPixels[i].Color;
            }
            return BaseColor;
        }
        set
        {
            EnsureInBounds(x, y);
            for (int i = ModifiedPixels.Count - 1; i >= 0; i--)
            {
                if (ModifiedPixels[i].X == x && ModifiedPixels[i].Y == y)
                    ModifiedPixels.RemoveAt(i);
            }
            ModifiedPixels.Add(new PixelPoint(x, y, value));
        }
    }

    // сохраняет копию текущего состояния перед изменением
    public void SaveStateForUndo()
    {
        var copy = new Photo
        {
            FilePath = FilePath,
            Width = Width,
            Height = Height,
            BaseColor = BaseColor
        };

        foreach (var p in ModifiedPixels)
            copy.ModifiedPixels.Add(new PixelPoint(p.X, p.Y, p.Color));
        foreach (var l in Labels)
            copy.Labels.Add(new TextLabel(l.Text, l.X, l.Y, l.Color));
        foreach (var c in CollageItems)
            copy.CollageItems.Add(new CollageItem(c.PhotoPath, c.X, c.Y, c.Width, c.Height));
        foreach (var ch in Changes)
            copy.Changes.Add(ch);

        History.Add(copy);
    }

    public string ToJson()
    {
        return JsonSerializer.Serialize(this, JsonOptions);
    }

    public static Photo FromJson(string json)
    {
        return JsonSerializer.Deserialize<Photo>(json)
            ?? throw new InvalidOperationException("Не удалось загрузить фото из JSON.");
    }
}
