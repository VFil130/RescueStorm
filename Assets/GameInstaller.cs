using UnityEngine;
using Zenject;
using Zenject.SpaceFighter;

public class GameInstaller : MonoInstaller
{
    public override void InstallBindings()
    {
        Container.Bind<NetworkService>().AsSingle();
        Container.Bind<AudioManager>().AsSingle();
        Container.Bind<ParticleManager>().AsSingle();
        Container.Bind<SceneManagerService>().AsSingle();
    }
}