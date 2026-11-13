using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace OfficeCore.Api.Controllers;

internal static class ControllerForbiddenExtensions
{
    public static ObjectResult ForbiddenMessage(this ControllerBase controller, string message)
        => controller.StatusCode(StatusCodes.Status403Forbidden, new { message });
}
