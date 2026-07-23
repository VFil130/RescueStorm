using Adrenak.UniVoice.Samples;
using Mirror;
using UnityEngine;
[RequireComponent(typeof(PlayerInputHandler))]
public class VoiceChatController : MonoBehaviour
{
    private PlayerInputHandler inputHandler;
    private bool isTalking;

    void Awake()
    {
        inputHandler = GetComponent<PlayerInputHandler>();
        inputHandler.OnTalkStarted.AddListener(StartTalking);
        inputHandler.OnTalkStopped.AddListener(StopTalking);
    }

    void OnDestroy()
    {
        if (inputHandler != null)
        {
            inputHandler.OnTalkStarted.RemoveListener(StartTalking);
            inputHandler.OnTalkStopped.RemoveListener(StopTalking);
        }
    }

    private void StartTalking()
    {
        if (!UniVoiceMirrorSetupSample.HasSetUp)
            return;

        var clientSession = UniVoiceMirrorSetupSample.ClientSession;
        if (clientSession != null && !isTalking)
        {
            clientSession.InputEnabled = true;
            isTalking = true;
        }
    }

    private void StopTalking()
    {
        if (!UniVoiceMirrorSetupSample.HasSetUp)
            return;

        var clientSession = UniVoiceMirrorSetupSample.ClientSession;
        if (clientSession != null && isTalking)
        {
            clientSession.InputEnabled = false;
            isTalking = false;
        }
    }

    public bool IsTalking()
    {
        return isTalking;
    }
}