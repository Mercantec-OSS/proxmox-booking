[ApiController]
[Route("authorization")]
public class AuthorizationController(
    UserRepository userRepository,
    UserSession session, 
    JwtTokenService jwt,
    EmailService emailService
    ) : ControllerBase
{
    [HttpGet("check-session")]
    public ActionResult<UserGetDto?> GetUser()
    {
        User user = session.User;
        return Ok(user.MakeGetDto());
    }

    [HttpPost("login")]
    public async Task<ActionResult<TokenDto>> PostToken(UserLoginDto userDto)
    {
        User? user = await userRepository.GetAsync(userDto.Email);

        if (user == null)
        {
            return Unauthorized(ResponseMessage.GetErrorMessage("Intern error. User not found."));
        }

        if (user.Password == null)
        {
            return Unauthorized(ResponseMessage.GetErrorMessage("Intern error. User password not found."));
        }

        if (!Password.Verify(userDto.Password, user.Password))
        {
            return Unauthorized(ResponseMessage.GetWrongCredentials());
        }

        string token = jwt.CreateToken(user);

        HttpContext.Response.Cookies.Append("token", token);
        return Ok(new TokenDto { Token = token });
    }

    [HttpDelete("logout")]
    [ProducesResponseType(204)]
    public ActionResult<bool> DeleteToken()
    {
        HttpContext.Response.Cookies.Delete("token");
        return NoContent();
    }
    [HttpPost("invite")]
    [ProducesResponseType(200)]
    public async Task<ActionResult<UserGetDto>> CreateInvitation(InviteDto dto){
        User user = session.GetIfRoles(Models.User.UserRoles.Admin, Models.User.UserRoles.Teacher);
        if (dto == null)
        {
            return BadRequest(ResponseMessage.GetErrorMessage("User dto not valid."));
        }

        if (await userRepository.GetAsync(dto.Email) != null)
        {
            return UnprocessableEntity(ResponseMessage.GetErrorMessage("User exist."));
        }

        // preevent to invite admin from teacher
        if (user.IsTeacher() && dto.Role.ToLower() == Models.User.UserRoles.Admin.ToString().ToLower())
        {
            return Unauthorized(ResponseMessage.GetUserUnauthorized());
        }

        // check for valid role
        List<string> availableRoles = new() { Models.User.UserRoles.Admin.ToString().ToLower(), Models.User.UserRoles.Teacher.ToString().ToLower() };
        if (!availableRoles.Contains(dto.Role.ToLower()))
        {
            return BadRequest(ResponseMessage.GetErrorMessage("Role not valid."));
        }

        User.UserRoles role = Models.User.UserRoles.Teacher;
        InviteToken token = InviteKeysService.CreateToken(dto.Email, dto.Role);

        // define admin user role
        if (dto.Role.ToLower() == Models.User.UserRoles.Admin.ToString().ToLower())
        {
            role = Models.User.UserRoles.Admin;
        }

        EmailDto email = EmailDto.GetInviteLink(dto.Email, token.Token, role.ToString());
        await emailService.SendAsync(email);

        return Ok($"Invitation sent to {dto.Email}");
    }

    [HttpPost("create")]
    [ProducesResponseType(201)]
    public async Task<ActionResult<UserGetDto>> PostCreate(UserCreateDto userDto)
    {
        if (userDto == null)
        {
            return BadRequest(ResponseMessage.GetErrorMessage("User dto not valid."));
        }

        if (await userRepository.GetAsync(userDto.Email) != null)
        {
            return UnprocessableEntity(ResponseMessage.GetErrorMessage("User need Unique Email."));
        }

        // define user role
        User.UserRoles userRole = Models.User.UserRoles.Student;

        // if first user in system set as admin
        if (await userRepository.GetUserCountAsync() == 0)
        {
            Console.WriteLine("First user in system. Set as Admin");
            userRole = Models.User.UserRoles.Admin;
        }

        InviteToken? inviteToken = InviteKeysService.UseToken(userDto.Email, userDto.InviteKey);
        if (userDto.InviteKey != "" && inviteToken != null)
        {
            if (inviteToken.Role == Models.User.UserRoles.Admin.ToString())
            {
                userRole = Models.User.UserRoles.Admin;
            }
            else if (inviteToken.Role == Models.User.UserRoles.Teacher.ToString())
            {
                userRole = Models.User.UserRoles.Teacher;
            }
        }

        userDto.GroupId = null; //NEED TO REMOVE
        return Ok(await CreateUser(userDto, userRole));
    }

    private async Task<UserGetDto> CreateUser(UserCreateDto userDto, User.UserRoles userRole)
    {
        User newUser = new()
        {
            Id = 0,
            Name = userDto.Name.Trim(),
            Surname = userDto.Surname.Trim(),
            Email = userDto.Email.ToLower(),
            Role = userRole.ToString(),
            Password = Password.GetHash(userDto.Password),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        await userRepository.CreateAsync(newUser);

        EmailDto email = EmailDto.GetUserCreation(newUser);
        await emailService.SendAsync(email);

        return newUser.MakeGetDto();
    }
}
