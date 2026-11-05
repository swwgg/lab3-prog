namespace PhotoViewer;

// сервис обработки фотографий (не зависит от консоли)
public static class PhotoEditor
{
    // меняет цвет отдельного пикселя
    public static void SetPixel(Photo photo, int x, int y, Pixel color)
    {
        photo.EnsureInBounds(x, y);
        photo.SaveStateForUndo();
        photo[x, y] = color;
        photo.Changes.Add($"Изменён пиксель ({x}, {y}) -> {color}");
    }

    // добавляет текстовую надпись
    public static void AddText(Photo photo, string text, int x, int y, Pixel color)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Текст надписи не может быть пустым.");
        photo.EnsureInBounds(x, y);

        photo.SaveStateForUndo();
        photo.Labels.Add(new TextLabel(text, x, y, color));
        photo.Changes.Add($"Добавлена надпись \"{text}\" в ({x}, {y}), цвет {color}");
    }

    // обрезает фотографию по прямоугольной области
    public static Photo Crop(Photo photo, int x, int y, int width, int height)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Ширина и высота должны быть положительными.");
        if (x < 0 || y < 0 || x + width > photo.Width || y + height > photo.Height)
            throw new ArgumentOutOfRangeException(nameof(x), "Область обрезки выходит за пределы фотографии.");

        photo.SaveStateForUndo();
        photo.Width = width;
        photo.Height = height;

        var newPixels = new List<PixelPoint>();
        foreach (var p in photo.ModifiedPixels)
        {
            int nx = p.X - x;
            int ny = p.Y - y;
            if (photo.IsInBounds(nx, ny))
                newPixels.Add(new PixelPoint(nx, ny, p.Color));
        }
        photo.ModifiedPixels = newPixels;

        var newLabels = new List<TextLabel>();
        foreach (var l in photo.Labels)
        {
            int nx = l.X - x;
            int ny = l.Y - y;
            if (photo.IsInBounds(nx, ny))
                newLabels.Add(new TextLabel(l.Text, nx, ny, l.Color));
        }
        photo.Labels = newLabels;

        photo.Changes.Add($"Обрезка: область ({x}, {y}, {width}x{height})");
        return photo;
    }

    // поворачивает фотографию на 90 градусов (вправо по часовой или влево против часовой)
    public static Photo Rotate(Photo photo, bool clockwise = true)
    {
        photo.SaveStateForUndo();

        int oldWidth = photo.Width;
        int oldHeight = photo.Height;
        photo.Width = oldHeight;
        photo.Height = oldWidth;

        foreach (var p in photo.ModifiedPixels)
        {
            int newX = clockwise ? oldHeight - 1 - p.Y : p.Y;
            int newY = clockwise ? p.X : oldWidth - 1 - p.X;
            p.X = newX;
            p.Y = newY;
        }

        foreach (var l in photo.Labels)
        {
            int newX = clockwise ? oldHeight - 1 - l.Y : l.Y;
            int newY = clockwise ? l.X : oldWidth - 1 - l.X;
            l.X = newX;
            l.Y = newY;
        }

        photo.Changes.Add(clockwise ? "Поворот на 90° вправо" : "Поворот на 90° влево");
        return photo;
    }

    // создаёт коллаж из нескольких фотографий на общем холсте
    public static Photo CreateCollage(
        string outputFilePath,
        int canvasWidth,
        int canvasHeight,
        Pixel background,
        List<(Photo Photo, int X, int Y)> items)
    {
        var canvas = new Photo(outputFilePath, canvasWidth, canvasHeight, background);

        foreach (var item in items)
        {
            var part = item.Photo;
            int offsetX = item.X;
            int offsetY = item.Y;

            canvas.CollageItems.Add(new CollageItem(part.FilePath, offsetX, offsetY, part.Width, part.Height));

            foreach (var pt in part.ModifiedPixels)
            {
                int tx = offsetX + pt.X;
                int ty = offsetY + pt.Y;
                if (canvas.IsInBounds(tx, ty))
                    canvas[tx, ty] = pt.Color;
            }

            foreach (var l in part.Labels)
            {
                int lx = offsetX + l.X;
                int ly = offsetY + l.Y;
                if (canvas.IsInBounds(lx, ly))
                    canvas.Labels.Add(new TextLabel(l.Text, lx, ly, l.Color));
            }
        }

        canvas.Changes.Add($"Создан коллаж ({canvasWidth}x{canvasHeight}) из {canvas.CollageItems.Count} фото");
        return canvas;
    }

    // фильтр осветления / затемнения
    public static void AdjustBrightness(Photo photo, int delta)
    {
        AdjustColors(photo, delta, delta, delta, logChange: false);
        string text = delta >= 0 ? $"Фильтр: Осветление (+{delta})" : $"Фильтр: Затемнение ({delta})";
        photo.Changes.Add(text);
    }

    // фильтр изменения каналов RGB
    public static void AdjustColors(Photo photo, int rDelta, int gDelta, int bDelta, bool logChange = true)
    {
        photo.SaveStateForUndo();
        photo.BaseColor = Pixel.FromRgb(photo.BaseColor.R + rDelta, photo.BaseColor.G + gDelta, photo.BaseColor.B + bDelta);

        foreach (var p in photo.ModifiedPixels)
            p.Color = Pixel.FromRgb(p.Color.R + rDelta, p.Color.G + gDelta, p.Color.B + bDelta);

        if (logChange)
            photo.Changes.Add($"Фильтр изменения цветов (dR={rDelta}, dG={gDelta}, dB={bDelta})");
    }

    // фильтр инверсии цветов
    public static void InvertColors(Photo photo)
    {
        photo.SaveStateForUndo();
        photo.BaseColor = InvertPixel(photo.BaseColor);

        foreach (var p in photo.ModifiedPixels)
            p.Color = InvertPixel(p.Color);

        photo.Changes.Add("Фильтр: Инверсия цветов");
    }

    private static Pixel InvertPixel(Pixel p)
    {
        return new Pixel((byte)(255 - p.R), (byte)(255 - p.G), (byte)(255 - p.B));
    }

    // фильтр оттенков серого
    public static void ApplyGrayscale(Photo photo)
    {
        photo.SaveStateForUndo();
        photo.BaseColor = ToGray(photo.BaseColor);

        foreach (var p in photo.ModifiedPixels)
            p.Color = ToGray(p.Color);

        photo.Changes.Add("Фильтр: Оттенки серого");
    }

    private static Pixel ToGray(Pixel p)
    {
        byte gray = (byte)Math.Clamp((p.R * 30 + p.G * 59 + p.B * 11) / 100, 0, 255);
        return new Pixel(gray, gray, gray);
    }

    // откатывает последнее изменение фотографии
    public static void Undo(Photo photo)
    {
        if (photo.History.Count == 0)
            throw new InvalidOperationException("Нет изменений для отката.");

        int lastIndex = photo.History.Count - 1;
        var prev = photo.History[lastIndex];
        photo.History.RemoveAt(lastIndex);

        photo.Width = prev.Width;
        photo.Height = prev.Height;
        photo.BaseColor = prev.BaseColor;
        photo.ModifiedPixels = prev.ModifiedPixels;
        photo.Labels = prev.Labels;
        photo.CollageItems = prev.CollageItems;
        photo.Changes = prev.Changes;
    }
}
