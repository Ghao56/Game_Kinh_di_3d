// Interface chung cho mọi thứ có thể bị làm choáng (các loại quái khác nhau).
// Script quái nào muốn bị muối làm choáng chỉ cần "implement" interface này.
public interface IStunnable
{
    void Stun(float duration);
}