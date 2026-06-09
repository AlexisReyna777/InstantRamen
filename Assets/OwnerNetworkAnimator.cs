using Unity.Netcode.Components;

public class OwnerNetworkAnimator : NetworkAnimator
{
    // Este truco le dice a Netcode que el dueño del personaje controla sus propias animaciones
    protected override bool OnIsServerAuthoritative()
    {
        return false;
    }
}
