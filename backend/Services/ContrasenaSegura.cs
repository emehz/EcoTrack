using System.Text;
using EcoTrack.Common;

namespace EcoTrack.Services;

public static class ContrasenaSegura
{
    private const int CostoBcrypt = 12;
    private const int LargoMinimo = 8;
    private const int BytesMaximos = 72;

    public static void Validar(string? contrasena, string campo, List<ErrorDetalle> errores)
    {
        if (string.IsNullOrEmpty(contrasena) || contrasena.Length < LargoMinimo)
        {
            errores.Add(new ErrorDetalle(campo, "Usa al menos 8 caracteres."));
            return;
        }

        if (Encoding.UTF8.GetByteCount(contrasena) > BytesMaximos)
        {
            errores.Add(new ErrorDetalle(campo, "La contraseña es demasiado larga."));
            return;
        }

        if (!contrasena.Any(char.IsLetter) || !contrasena.Any(char.IsDigit))
        {
            errores.Add(new ErrorDetalle(campo, "Combina letras y números."));
        }
    }

    public static string Hash(string contrasena) => BCrypt.Net.BCrypt.HashPassword(contrasena, CostoBcrypt);

    public static bool Coincide(string contrasena, string hash) => BCrypt.Net.BCrypt.Verify(contrasena, hash);
}
