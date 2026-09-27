using TaneHesap.Application.DailySales;

namespace TaneHesap.Application.IncomeVerification;

/// <summary>
/// Dükkân içi gerçek gelire bağlı türetilmiş kayıt (kasaya yazılan gelir, POS komisyonu). Bir gün doğrulanınca
/// ya da doğrulaması kaldırılınca yalnızca bunlar yeniden hesaplanır (stok düşümü gibi satışa bağlı olanlar değil).
/// </summary>
public interface IIncomeDependentSideEffect : IDailySalesSideEffect
{
}
