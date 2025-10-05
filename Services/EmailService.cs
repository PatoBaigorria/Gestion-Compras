using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Gestion_Compras.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;
        private readonly HttpClient _httpClient;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
            _httpClient = new HttpClient();
        }

        public async Task<bool> EnviarEmailAsync(string destinatario, string asunto, string cuerpo)
        {
            try
            {
                var apiKey = _configuration["Resend:ApiKey"];
                var fromEmail = _configuration["Resend:FromEmail"] ?? "onboarding@resend.dev";
                var fromName = _configuration["Resend:FromName"] ?? "Sistema Gestión Pañol";

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                var emailData = new
                {
                    from = $"{fromName} <{fromEmail}>",
                    to = new[] { destinatario },
                    subject = asunto,
                    html = cuerpo
                };

                var json = JsonSerializer.Serialize(emailData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("https://api.resend.com/emails", content);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"Email enviado exitosamente a {destinatario}");
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Error al enviar email a {destinatario}: {response.StatusCode} - {errorContent}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error al enviar email a {destinatario}: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> EnviarEmailRecuperacionConTokenAsync(string destinatario, string nombreUsuario, string resetLink)
        {
            var asunto = "Recuperación de Contraseña - Sistema Gestión Pañol";
            var cuerpo = $@"
                <html>
                <head>
                    <style>
                        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                        .header {{ background-color: #007bff; color: white; padding: 20px; text-align: center; border-radius: 5px 5px 0 0; }}
                        .content {{ background-color: #f8f9fa; padding: 30px; border-radius: 0 0 5px 5px; }}
                        .btn {{ display: inline-block; padding: 12px 30px; background-color: #007bff; color: white; text-decoration: none; border-radius: 5px; margin: 20px 0; }}
                        .warning {{ background-color: #fff3cd; border-left: 4px solid #ffc107; padding: 15px; margin: 20px 0; }}
                        .footer {{ text-align: center; margin-top: 20px; font-size: 12px; color: #6c757d; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='header'>
                            <h1>Recuperación de Contraseña</h1>
                        </div>
                        <div class='content'>
                            <p>Hola <strong>{nombreUsuario}</strong>,</p>
                            <p>Hemos recibido una solicitud para recuperar tu contraseña.</p>
                            <p>Haz clic en el siguiente botón para crear una nueva contraseña:</p>
                            
                            <div style='text-align: center;'>
                                <a href='{resetLink}' class='btn'>Restablecer Contraseña</a>
                            </div>
                            
                            <div class='warning'>
                                <strong>⚠️ Importante:</strong>
                                <ul>
                                    <li>Este enlace es válido por 5 minutos</li>
                                    <li>Si no solicitaste este cambio, ignora este email</li>
                                    <li>Por seguridad, nunca compartas este enlace</li>
                                </ul>
                            </div>
                            
                            <p style='font-size: 12px; color: #6c757d;'>Si el botón no funciona, copia y pega este enlace en tu navegador:</p>
                            <p style='font-size: 11px; word-break: break-all; color: #007bff;'>{resetLink}</p>
                        </div>
                        <div class='footer'>
                            <p>Este es un mensaje automático, por favor no responder.</p>
                            <p>&copy; {DateTime.Now.Year} Sistema Gestión Pañol</p>
                        </div>
                    </div>
                </body>
                </html>";

            return await EnviarEmailAsync(destinatario, asunto, cuerpo);
        }
    }
}
