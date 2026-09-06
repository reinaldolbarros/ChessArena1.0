using ChessMAUI.Services;

namespace ChessMAUI.Views;

public partial class SubscriptionPage : ContentPage
{
    private SubscriptionService Sub => AppState.Current.Subscription;

    public SubscriptionPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        RefreshUI();
    }

    private void RefreshUI()
    {
        var tier = Sub.ActiveTier;

        CurrentPlanLabel.Text = $"Plano atual: {Sub.StatusText}";

        FreeBadge.IsVisible  = tier == SubscriptionTier.Free;
        PrataBadge.IsVisible = tier == SubscriptionTier.Prata;
        OuroBadge.IsVisible  = tier == SubscriptionTier.Ouro;

        PrataBtn.Text = tier == SubscriptionTier.Prata
            ? "Renovar Challenger — R$ 5,90/mês"
            : "Assinar Challenger — R$ 5,90/mês";

        OuroBtn.Text = tier == SubscriptionTier.Ouro
            ? "Renovar Grande Mestre — R$ 9,90/mês"
            : "Assinar Grande Mestre — R$ 9,90/mês";

        // Desativa botão do plano que não faz upgrade (Prata não aparece se já é Ouro)
        PrataBtn.IsVisible = tier != SubscriptionTier.Ouro;

        CancelLink.IsVisible = Sub.IsActive;
    }

    private async void OnPrataClicked(object? sender, EventArgs e)
        => await ConfirmSubscribe(SubscriptionTier.Prata, "Challenger", "R$ 5,90/mês");

    private async void OnOuroClicked(object? sender, EventArgs e)
        => await ConfirmSubscribe(SubscriptionTier.Ouro, "Grande Mestre", "R$ 9,90/mês");

    private async Task ConfirmSubscribe(SubscriptionTier tier, string label, string price)
    {
        bool ok = await DisplayAlert(
            $"Assinar Plano {label}",
            $"Confirmar assinatura de {price}?\n\nEm produção esta tela seria substituída pela loja do dispositivo (Google Play / App Store).",
            "Confirmar", "Cancelar");

        if (!ok) return;

        Sub.Subscribe(tier);

        await DisplayAlert("Pronto!", $"Plano {label} ativo até {Sub.ExpiresAt:dd/MM/yyyy}.", "OK");
        RefreshUI();
    }

    private async void OnCancelClicked(object? sender, TappedEventArgs e)
    {
        bool ok = await DisplayAlert("Cancelar assinatura",
            "Tem certeza? Você perderá os benefícios do plano.", "Cancelar plano", "Manter");
        if (!ok) return;

        Sub.Cancel();
        await DisplayAlert("Cancelado", "Sua assinatura foi cancelada.", "OK");
        RefreshUI();
    }
}
