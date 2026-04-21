using ProfileModel = aether_garden_be.Models.Profile;

namespace aether_garden_be.Services.Profile;

public class StaticProfileProvider : IProfileProvider
{
    private static readonly ProfileModel Profile = new(
        Name: "Duck Ran",
        Title: "计算机系学生 / Student Builder",
        Bio: "来自江西，目前在武汉大学学习。偏好做能解决真实问题的小而美应用，也喜欢在学习过程中记录思路与细节。",
        Location: "Jiangxi, China",
        School: "Wuhan University",
        Website: "https://laipuran.github.io/",
        Github: "https://github.com/laipuran",
        Interests: ["C# 与 .NET", "Web 全栈开发", "小工具产品化", "算法与数据结构"],
        ContactEmail: "puranlai@qq.com"
    );

    public ProfileModel GetProfile() => Profile;
}
