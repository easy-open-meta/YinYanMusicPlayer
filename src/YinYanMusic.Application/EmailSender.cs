using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;

namespace YinYanMusic.Application;

/// <summary>
/// SMTP 配置（配置节 <c>Smtp</c>）。
/// <para>
/// ⚠️ 刻意做成"可缺省"：部署形态有 Docker / Windows 安装包 / 本机开发三种，
/// 不能让"没配 SMTP"阻断整个 API 启动。未配置时 <see cref="IsConfigured"/> 为 false，
/// 邮箱绑定相关接口返回明确提示，客户端据此隐藏入口（见 V2.5 设计文档）。
/// </para>
/// </summary>
public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public string? UserName { get; set; }
    public string? Password { get; set; }

    /// <summary>发件人地址；留空则回退用 <see cref="UserName"/>。</summary>
    public string? FromAddress { get; set; }

    /// <summary>发件人显示名。</summary>
    public string FromName { get; set; } = "音言音乐";

    /// <summary>
    /// 是否用 SSL/TLS。默认 true（隐式 TLS，465 端口常见）；
    /// 587 端口用 STARTTLS 时应设 false，MailKit 会自动协商 STARTTLS。
    /// </summary>
    public bool UseSsl { get; set; } = true;

    /// <summary>三个必填项齐了才算配置好。</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(UserName) && !string.IsNullOrWhiteSpace(Password);

    /// <summary>实际发件人地址。</summary>
    public string EffectiveFrom => string.IsNullOrWhiteSpace(FromAddress) ? UserName! : FromAddress;
}

/// <summary>发信结果：失败时带可读原因，便于接口层原样回给客户端。</summary>
public record EmailSendResult(bool Success, string? Error = null);

public interface IEmailSender
{
    /// <summary>SMTP 是否已配置。未配置时 <see cref="SendAsync"/> 会直接返回失败。</summary>
    bool IsConfigured { get; }

    Task<EmailSendResult> SendAsync(string to, string subject, string body, CancellationToken ct = default);
}

/// <summary>
/// 基于 MailKit 的 SMTP 发信实现。
/// 选 MailKit 而非 <c>System.Net.Mail.SmtpClient</c>：后者已被官方标记过时
/// （不支持现代 TLS 协商，部分服务商直接拒发）。
/// </summary>
public sealed class SmtpEmailSender(SmtpOptions options) : IEmailSender
{
    public bool IsConfigured => options.IsConfigured;

    public async Task<EmailSendResult> SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        if (!options.IsConfigured)
            return new EmailSendResult(false, "服务器未配置邮件服务，暂无法发送验证码。");

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(options.FromName, options.EffectiveFrom));
            message.To.Add(MailboxAddress.Parse(to));
            message.Subject = subject;
            message.Body = new TextPart("plain") { Text = body };

            using var client = new SmtpClient();
            // UseSsl=true → 隐式 TLS（SslOnConnect）；false → 自动协商 STARTTLS。
            // 用 Auto 在明文端口上也能工作，避免"端口对但模式错"这种配置坑。
            var security = options.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.Auto;
            await client.ConnectAsync(options.Host!, options.Port, security, ct);
            await client.AuthenticateAsync(options.UserName!, options.Password!, ct);
            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);
            return new EmailSendResult(true);
        }
        catch (Exception ex)
        {
            // 不把 SMTP 原始异常抛给客户端（可能含服务器地址/账号信息），只记日志 + 回可读提示
            return new EmailSendResult(false, $"邮件发送失败：{ex.Message}");
        }
    }
}
