using ProfileModel = aether_garden_be.Models.Profile;

namespace aether_garden_be.Services.Profile;

public class StaticProfileProvider
{
    private static readonly ProfileModel Profile = new(
        Name: "Duck Ran",
        Title: "计算机系学生 / CS Student",
        Bio: "热爱开源，想研究一些有趣的东西。",
        Location: "Hubei, China",
        School: "Wuhan University",
        Website: "https://duckran.top/",
        Github: "https://github.com/laipuran",
        Interests: ["同人音乐", "钢琴", "音乐游戏", "C# 和 .NET"],
        ContactEmail: "puranlai@qq.com"
    );

    public ProfileModel GetProfile() => Profile;
}
