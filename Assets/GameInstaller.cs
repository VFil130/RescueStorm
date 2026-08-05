using UnityEngine;
using Zenject;
using Zenject.SpaceFighter;

public class GameInstaller : MonoInstaller
{
    public override void InstallBindings()
    {
        Container.Bind<AudioManager>().AsSingle();
        Container.Bind<ParticleManager>().AsSingle();
        Container.Bind<SceneManagerService>().AsSingle();
        Container.Bind<LanguageManager>().AsSingle().NonLazy();
    }
}