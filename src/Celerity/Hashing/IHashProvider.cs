namespace Celerity.Hashing;

public interface IHashProvider<T>
{
    T Hash(T key);
}
