namespace PhotoViewer.Tests;

public class PhotoViewerTests
{
    [Fact]
    public void CreatePhoto_StoresFilePathAndDimensions()
    {
        var fill = new Pixel(10, 20, 30);
        var photo = new Photo("images/photo1.jpg", 800, 600, fill);

        Assert.Equal("images/photo1.jpg", photo.FilePath);
        Assert.Equal(800, photo.Width);
        Assert.Equal(600, photo.Height);
        Assert.Equal(fill, photo[0, 0]);
        Assert.Equal(fill, photo[799, 599]);
        Assert.Throws<ArgumentException>(() => new Photo("", 800, 600));
        Assert.Throws<ArgumentException>(() => new Photo("500", 800, 600));
        Assert.Throws<FileNotFoundException>(() => Photo.Open("missing_photo.jpg"));
    }

    [Fact]
    public void SetPixel_ChangesTargetPixelAndLogsChange()
    {
        var photo = new Photo("images/test.png", 100, 100);
        var red = new Pixel(255, 0, 0);

        PhotoEditor.SetPixel(photo, 10, 20, red);

        Assert.Equal(red, photo[10, 20]);
        Assert.Equal(Pixel.Black, photo[0, 0]);
        Assert.Single(photo.Changes);
        Assert.Throws<ArgumentOutOfRangeException>(() => PhotoEditor.SetPixel(photo, 100, 0, red));
    }

    [Fact]
    public void AddText_AddsLabelAndValidatesArguments()
    {
        var photo = new Photo("images/test.png", 200, 200);
        PhotoEditor.AddText(photo, "Отпуск 2026", 20, 30, Pixel.White);

        Assert.Single(photo.Labels);
        Assert.Equal("Отпуск 2026", photo.Labels[0].Text);
        Assert.Equal(20, photo.Labels[0].X);
        Assert.Equal(30, photo.Labels[0].Y);
        Assert.Single(photo.Changes);

        Assert.Throws<ArgumentException>(() => PhotoEditor.AddText(photo, " ", 0, 0, Pixel.White));
        Assert.Throws<ArgumentOutOfRangeException>(() => PhotoEditor.AddText(photo, "Текст", 300, 0, Pixel.White));
    }

    [Fact]
    public void Crop_ExtractsSubRegionAndShiftsLabelsAndPixels()
    {
        var photo = new Photo("images/orig.jpg", 4, 4);
        PhotoEditor.SetPixel(photo, 1, 1, new Pixel(100, 150, 200));
        PhotoEditor.SetPixel(photo, 2, 2, new Pixel(50, 60, 70));
        PhotoEditor.AddText(photo, "Внутри", 2, 2, Pixel.White);
        PhotoEditor.AddText(photo, "Снаружи", 0, 0, Pixel.White);

        var cropped = PhotoEditor.Crop(photo, 1, 1, 2, 2);

        Assert.Equal("images/orig.jpg", cropped.FilePath);
        Assert.Equal(2, cropped.Width);
        Assert.Equal(2, cropped.Height);
        Assert.Equal(new Pixel(100, 150, 200), cropped[0, 0]);
        Assert.Equal(new Pixel(50, 60, 70), cropped[1, 1]);

        Assert.Single(cropped.Labels);
        Assert.Equal("Внутри", cropped.Labels[0].Text);
        Assert.Equal(1, cropped.Labels[0].X);
        Assert.Equal(1, cropped.Labels[0].Y);
    }

    [Fact]
    public void Rotate_RotatesPixelsAndLabelsRightAndLeft()
    {
        var photo = new Photo("images/rect.jpg", 3, 2);
        PhotoEditor.SetPixel(photo, 0, 0, new Pixel(255, 0, 0));
        PhotoEditor.SetPixel(photo, 2, 1, new Pixel(0, 255, 0));
        PhotoEditor.AddText(photo, "Верх", 0, 0, Pixel.White);

        // поворот на 90 градусов вправо (по часовой стрелке)
        PhotoEditor.Rotate(photo, clockwise: true);

        Assert.Equal(2, photo.Width);
        Assert.Equal(3, photo.Height);
        Assert.Equal(new Pixel(255, 0, 0), photo[1, 0]);
        Assert.Equal(new Pixel(0, 255, 0), photo[0, 2]);
        Assert.Equal(1, photo.Labels[0].X);
        Assert.Equal(0, photo.Labels[0].Y);

        // поворот на 90 градусов влево возвращает исходное положение
        PhotoEditor.Rotate(photo, clockwise: false);
        Assert.Equal(3, photo.Width);
        Assert.Equal(2, photo.Height);
        Assert.Equal(new Pixel(255, 0, 0), photo[0, 0]);
        Assert.Equal(new Pixel(0, 255, 0), photo[2, 1]);
    }

    [Fact]
    public void CreateCollage_ComposesMultiplePhotosAndTracksItems()
    {
        var p1 = new Photo("images/part1.jpg", 100, 100);
        PhotoEditor.SetPixel(p1, 0, 0, new Pixel(255, 0, 0));

        var p2 = new Photo("images/part2.jpg", 100, 100);
        PhotoEditor.SetPixel(p2, 5, 5, new Pixel(0, 0, 255));
        PhotoEditor.AddText(p2, "Подпись", 10, 10, Pixel.White);

        var items = new List<(Photo, int, int)> { (p1, 0, 0), (p2, 100, 50) };
        var collage = PhotoEditor.CreateCollage("images/collage.jpg", 300, 200, Pixel.Black, items);

        Assert.Equal("images/collage.jpg", collage.FilePath);
        Assert.Equal(300, collage.Width);
        Assert.Equal(200, collage.Height);
        Assert.Equal(2, collage.CollageItems.Count);
        Assert.Equal("images/part1.jpg", collage.CollageItems[0].PhotoPath);
        Assert.Equal("images/part2.jpg", collage.CollageItems[1].PhotoPath);
        Assert.Equal(new Pixel(255, 0, 0), collage[0, 0]);
        Assert.Equal(new Pixel(0, 0, 255), collage[105, 55]);
        Assert.Single(collage.Labels);
        Assert.Equal(110, collage.Labels[0].X);
        Assert.Equal(60, collage.Labels[0].Y);
    }

    [Fact]
    public void Filters_ModifyColorsAndLogChanges()
    {
        var photo = new Photo("images/filter.jpg", 10, 10, new Pixel(100, 150, 200));
        PhotoEditor.SetPixel(photo, 2, 2, new Pixel(50, 100, 150));

        PhotoEditor.AdjustBrightness(photo, 70);
        Assert.Equal(new Pixel(170, 220, 255), photo[0, 0]);
        Assert.Equal(new Pixel(120, 170, 220), photo[2, 2]);

        PhotoEditor.AdjustColors(photo, -70, -20, -55);
        Assert.Equal(new Pixel(100, 200, 200), photo[0, 0]);

        PhotoEditor.InvertColors(photo);
        Assert.Equal(new Pixel(155, 55, 55), photo[0, 0]);

        PhotoEditor.ApplyGrayscale(photo);
        Assert.Equal(photo[0, 0].R, photo[0, 0].G);
        Assert.Equal(photo[0, 0].G, photo[0, 0].B);
        Assert.Equal(5, photo.Changes.Count);
    }

    [Fact]
    public void SerializationAndAppState_PersistsAndRestoresFullState()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"photoviewer_test_{Guid.NewGuid()}.json");
        try
        {
            var state = new AppState();
            var photo = new Photo("C:/Photos/summer.jpg", 1920, 1080, new Pixel(12, 34, 56));
            PhotoEditor.SetPixel(photo, 20, 10, new Pixel(250, 200, 150));
            PhotoEditor.AddText(photo, "Лето", 100, 50, Pixel.White);
            PhotoEditor.AdjustBrightness(photo, 15);

            state.Photos.Add(photo);
            state.CurrentIndex = 0;

            state.SaveToFile(tempFile);

            var restored = AppState.LoadFromFile(tempFile);
            var restoredPhoto = restored.GetCurrentPhoto();

            Assert.NotNull(restoredPhoto);
            Assert.Equal("C:/Photos/summer.jpg", restoredPhoto.FilePath);
            Assert.Equal(1920, restoredPhoto.Width);
            Assert.Equal(1080, restoredPhoto.Height);
            Assert.Equal(new Pixel(27, 49, 71), restoredPhoto[0, 0]);
            Assert.Equal(new Pixel(255, 215, 165), restoredPhoto[20, 10]);
            Assert.Single(restoredPhoto.Labels);
            Assert.Equal("Лето", restoredPhoto.Labels[0].Text);
            Assert.Equal(3, restoredPhoto.Changes.Count);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void CloseCurrentPhoto_RemovesPhotoAndUpdatesIndex()
    {
        var state = new AppState();
        state.Photos.Add(new Photo("images/first.jpg", 100, 100));
        state.Photos.Add(new Photo("images/second.jpg", 100, 100));
        state.CurrentIndex = 1;

        state.CloseCurrentPhoto();

        Assert.Single(state.Photos);
        Assert.Equal(0, state.CurrentIndex);
        Assert.Equal("images/first.jpg", state.GetCurrentPhoto()?.FilePath);

        state.CloseCurrentPhoto();

        Assert.Empty(state.Photos);
        Assert.Null(state.GetCurrentPhoto());
        Assert.Throws<InvalidOperationException>(() => state.CloseCurrentPhoto());
    }

    [Fact]
    public void Undo_RevertsLastOperationsStepByStep()
    {
        var photo = new Photo("images/undo.jpg", 10, 8);
        PhotoEditor.SetPixel(photo, 1, 1, new Pixel(255, 0, 0));
        PhotoEditor.AddText(photo, "Надпись", 2, 2, Pixel.White);
        photo = PhotoEditor.Rotate(photo, clockwise: true);

        Assert.Equal(8, photo.Width);
        Assert.Equal(10, photo.Height);
        Assert.Equal(3, photo.Changes.Count);

        // откат поворота
        PhotoEditor.Undo(photo);
        Assert.Equal(10, photo.Width);
        Assert.Equal(8, photo.Height);
        Assert.Single(photo.Labels);
        Assert.Equal(2, photo.Changes.Count);

        // откат добавления надписи
        PhotoEditor.Undo(photo);
        Assert.Empty(photo.Labels);
        Assert.Equal(new Pixel(255, 0, 0), photo[1, 1]);
        Assert.Single(photo.Changes);

        // откат изменения пикселя
        PhotoEditor.Undo(photo);
        Assert.Equal(Pixel.Black, photo[1, 1]);
        Assert.Empty(photo.Changes);

        Assert.Throws<InvalidOperationException>(() => PhotoEditor.Undo(photo));
    }
}
