using ChessMAUI.Services;

namespace ChessMAUI.Views;

public partial class ProfilePage : ContentPage
{
    private static readonly string[] Avatars =
        ["♟","♛","♚","♜","♝","♞","🎯","🔥","💎","👑","🦁","🐉","⚡","🌟","🎭","🛡️"];

    private string _pendingAvatarPath = "";
    private string _pendingEmoji      = "";

    public ProfilePage()
    {
        InitializeComponent();
        CountryPicker.ItemsSource = GeoData.Countries.ToList();
        StatePicker.ItemsSource   = GeoData.BrazilStates.ToList();
        TakePhotoBtn.IsVisible    = MediaPicker.Default.IsCaptureSupported;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        var p = AppState.Current.Profile;
        p.EnsureCountryDefault();

        NameEntry.Text = p.Name;

        int ci = GeoData.Countries.IndexOf(p.Country);
        if (ci >= 0) CountryPicker.SelectedIndex = ci;

        bool isBrazil = p.Country == "Brasil";
        StateSection.IsVisible = isBrazil;
        if (isBrazil && !string.IsNullOrEmpty(p.State))
        {
            int si = GeoData.BrazilStates.FindIndex(s => s.StartsWith(p.State));
            if (si >= 0) StatePicker.SelectedIndex = si;
        }

        _pendingAvatarPath = p.AvatarPath;
        _pendingEmoji      = p.Avatar;

        RefreshAvatarDisplay();
    }

    private void OnCountryChanged(object? sender, EventArgs e)
    {
        bool isBrazil = CountryPicker.SelectedItem as string == "Brasil";
        StateSection.IsVisible = isBrazil;
        if (!isBrazil) StatePicker.SelectedIndex = -1;
    }

    private void RefreshAvatarDisplay()
    {
        bool isRemote = !string.IsNullOrEmpty(_pendingAvatarPath)
            && (_pendingAvatarPath.StartsWith("http://") || _pendingAvatarPath.StartsWith("https://"));
        bool hasLocalPhoto = !isRemote && !string.IsNullOrEmpty(_pendingAvatarPath) && File.Exists(_pendingAvatarPath);
        bool hasPhoto = isRemote || hasLocalPhoto;

        AvatarImage.IsVisible = hasPhoto;
        AvatarLabel.IsVisible = !hasPhoto;

        if (isRemote)
            AvatarImage.Source = ImageSource.FromUri(new Uri(_pendingAvatarPath));
        else if (hasLocalPhoto)
            AvatarImage.Source = ImageSource.FromFile(_pendingAvatarPath);
        else
            AvatarLabel.Text = string.IsNullOrEmpty(_pendingEmoji) ? "♟" : _pendingEmoji;
    }

    private async void OnPickEmojiClicked(object? sender, EventArgs e)
    {
        string? choice = await DisplayActionSheet("Escolha seu avatar", "Cancelar", null, Avatars);
        if (choice == null || choice == "Cancelar") return;

        _pendingEmoji      = choice;
        _pendingAvatarPath = "";          // limpa a foto se escolheu emoji
        RefreshAvatarDisplay();
    }

    private async void OnPickPhotoClicked(object? sender, EventArgs e)
    {
        try
        {
            var result = await MediaPicker.Default.PickPhotoAsync(new MediaPickerOptions
            {
                Title = "Escolha uma foto"
            });
            await SaveAvatarPhotoAsync(result);
        }
        catch (PermissionException)
        {
            await DisplayAlert("Permissão necessária",
                "Permita o acesso às fotos nas configurações do dispositivo.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Erro", $"Não foi possível carregar a foto: {ex.Message}", "OK");
        }
    }

    private async void OnTakePhotoClicked(object? sender, EventArgs e)
    {
        try
        {
            var result = await MediaPicker.Default.CapturePhotoAsync(new MediaPickerOptions
            {
                Title = "Tire uma foto"
            });
            await SaveAvatarPhotoAsync(result);
        }
        catch (PermissionException)
        {
            await DisplayAlert("Permissão necessária",
                "Permita o acesso à câmera nas configurações do dispositivo.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Erro", $"Não foi possível usar a câmera: {ex.Message}", "OK");
        }
    }

    private async Task SaveAvatarPhotoAsync(FileResult? result)
    {
        if (result == null) return;

        var destPath = Path.Combine(FileSystem.AppDataDirectory, "avatar_photo.jpg");

        using var src  = await result.OpenReadAsync();
        using var dest = File.OpenWrite(destPath);
        await src.CopyToAsync(dest);

        _pendingAvatarPath = destPath;
        RefreshAvatarDisplay();
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        var name = NameEntry.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(name))
        {
            await DisplayAlert("Nome obrigatório", "Por favor, informe seu nome.", "OK");
            return;
        }

        var p     = AppState.Current.Profile;
        p.Name    = name;
        p.Country = CountryPicker.SelectedItem as string ?? "";
        p.State   = GeoData.StateAbbr(StatePicker.SelectedItem as string);
        p.AvatarPath = _pendingAvatarPath;

        if (string.IsNullOrEmpty(_pendingAvatarPath))
            p.Avatar = string.IsNullOrEmpty(_pendingEmoji) ? "♟" : _pendingEmoji;

        // Sem isso, a edição ficava só no aparelho — Ranking e qualquer outro dispositivo
        // continuavam mostrando os dados antigos, porque só liam do Supabase.
        if (!AppState.Current.Auth.IsAnonymous)
            await p.SyncToSupabaseAsync();

        await DisplayAlert("Salvo", "Perfil atualizado com sucesso.", "OK");
    }
}
