using ProfileModel = aether_garden_be.Models.Profile;

namespace aether_garden_be.Services.Profile;

public interface IProfileProvider
{
    ProfileModel GetProfile();
}
