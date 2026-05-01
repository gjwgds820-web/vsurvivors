using Cysharp.Threading.Tasks;

public interface IAsyncInitializable
{
    UniTask InitAsync();
}
