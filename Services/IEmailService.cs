namespace Gestion_Compras.Services
{
    public interface IEmailService
    {
        Task<bool> EnviarEmailAsync(string destinatario, string asunto, string cuerpo);
        Task<bool> EnviarEmailRecuperacionConTokenAsync(string destinatario, string nombreUsuario, string resetLink);
    }
}
