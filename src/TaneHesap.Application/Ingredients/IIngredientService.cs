namespace TaneHesap.Application.Ingredients;

/// <summary>
/// Malzeme kataloğu ve güncel birim fiyat/stok bilgisi — ADMIN yönetir.
/// Fiyat güncellemeleri tabak maliyeti hesaplarını (bkz. Dishes modülü) doğrudan etkiler.
/// bkz. Proje Raporu bölüm 3.3, 3.9.
/// </summary>
public interface IIngredientService
{
    Task<List<IngredientDto>> GetAllAsync(Guid businessId, CancellationToken ct = default);

    Task<IngredientDto> GetByIdAsync(Guid businessId, Guid id, CancellationToken ct = default);

    Task<IngredientDto> CreateAsync(Guid businessId, CreateIngredientRequest request, Guid createdByUserId, CancellationToken ct = default);

    Task<IngredientDto> UpdateAsync(Guid businessId, Guid id, UpdateIngredientRequest request, Guid updatedByUserId, CancellationToken ct = default);

    /// <summary>Reçetede, stok hareketinde, alışta veya gün sonu kaydında hiç kullanılmamış malzemeyi siler.</summary>
    Task DeleteAsync(Guid businessId, Guid id, CancellationToken ct = default);

    /// <summary>Minimum eşiğin altında kalan (aktif) malzemeleri döner — düşük stok bildirimi için kullanılır.</summary>
    Task<List<IngredientDto>> GetBelowThresholdAsync(Guid businessId, CancellationToken ct = default);
}
