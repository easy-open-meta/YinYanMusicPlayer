using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace YinYanMusic.Api.Auth;

/// <summary>
/// 把 Bearer（JWT）安全方案写进 OpenAPI 文档，Scalar UI 上即可填 token 直接调试 admin 接口（P1）。
/// 方案名固定 "Bearer"，与 JWT Bearer 鉴权方案对应；文档仅在 OpenApi:Enabled 开启时生成，
/// 内容只有接口契约，不含连接串/密钥等敏感配置。
/// </summary>
internal sealed class BearerSecuritySchemeTransformer(IAuthenticationSchemeProvider authenticationSchemeProvider)
    : IOpenApiDocumentTransformer
{
    public async Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        var schemes = await authenticationSchemeProvider.GetAllSchemesAsync();
        if (!schemes.Any(s => s.Name == "Bearer"))
            return;

        // 文档级安全方案定义：UI 的 Authorize 按钮据此渲染 token 输入框
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "登录接口返回的 JWT，直接粘贴（不要带 Bearer 前缀）。admin 接口需要 admin 角色的 token。"
        };

        // 默认应用到全部操作：UI 每个接口都会带上已配置的 token
        // 注意：OpenApi 2.x 的引用序列化需要 hostDocument，否则会输出空对象
        document.Security ??= new List<OpenApiSecurityRequirement>();
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = new List<string>()
        });
    }
}
