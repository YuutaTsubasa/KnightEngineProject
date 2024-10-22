using System.Collections;
using OneOf;
using OneOf.Types;

namespace KnightEngine.Utils
{
    public struct Option<T> : IOneOf, IEnumerable<T>
    {
        private OneOf<None, T> _value;

        object IOneOf.Value => _value.Value; 
        int IOneOf.Index => _value.Index;

        public Option(T t)
        {
            _value = t;
        }

        public static Option<T> None()
        {
            var option = new Option<T>
            {
                _value = default(None)
            };
            return option;
        }

        public bool HasValue => _value.IsT1;

        public static implicit operator Option<T>(T t) => new (t);
        public static implicit operator Option<T>(OneOf<None, T> t) => t.Match(
            _ => None(),
            value => value);

        public void Switch(Action<T> valueAction, Action noneAction)
            => _value.Switch(_ => noneAction?.Invoke(), valueAction);

        public TResult Match<TResult>(Func<T, TResult> valueFunc, Func<TResult> noneFunc)
            => _value.Match(_ => noneFunc(), valueFunc);

        public void SwitchSome(Action<T> valueAction)
            => _value.Switch(_ => {}, valueAction);
        
        public void SwitchNone(Action noneAction)
            => _value.Switch(_ => noneAction?.Invoke(), _ => {});

        public Option<TResult> Map<TResult>(Func<T, TResult> valueFunc)
            => _value.MapT1(valueFunc);

        public Option<TResult> FlatMap<TResult>(Func<T, Option<TResult>> valueFunc)
            => Map(valueFunc).ValueOr(Option<TResult>.None());

        public T ValueOr(T defaultValue)
            => _value.IsT1
                ? _value.AsT1
                : defaultValue;

        public T ValueOrFailure()
            => _value.AsT1;
        
        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            if (_value.IsT0)
                yield break;

            yield return _value.AsT1;
        }

        IEnumerator IEnumerable.GetEnumerator()
            => (this as IEnumerable<T>).GetEnumerator();
    }

    public static class OptionExtension
    {
        public static Func<T, bool> GenerateNotResultFunc<T>(this Func<T, bool> func)
            => t => !func(t);

        public static Option<T> Some<T>(this T t)
            => t;

        public static Option<T> SomeNotNull<T>(this T t)
            => t.SomeWhen(x => x != null);
        
        public static Option<T> SomeWhen<T>(this T t, Func<T, bool> predicate)
            => predicate(t)
                ? t
                : Option<T>.None();

        public static Option<T> NoneWhen<T>(this T t, Func<T, bool> predicate)
            => t.SomeWhen(predicate.GenerateNotResultFunc());

        public static Option<T> FirstOrNone<T>(this IEnumerable<T> enumerable)
            => enumerable.FirstOrNone(_ => true);
        
        public static Option<T> FirstOrNone<T>(this IEnumerable<T> enumerable, Func<T, bool> predicate)
            => enumerable
                .Where(predicate)
                .NoneWhen(enumerable => enumerable.Count() == 0)
                .Map(enumerable => enumerable.First());

        public static Option<T> Or<T>(this Option<T> tOption, Option<T> orOption)
            => tOption.Match(
                option => option,
                () => orOption);
        
        public static Option<TValue> GetValueOrNone<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key)
            => dictionary.ContainsKey(key)
                ? dictionary[key]
                : Option<TValue>.None();

        public static Option<T> GetValueOrNone<T>(this IEnumerable<T> enumerable, int index)
            => enumerable.Skip(index).FirstOrNone();

        public static T[] Values<T>(this IEnumerable<Option<T>> enumerable)
            => enumerable
                .Where(element => element.HasValue)
                .Select(element => element.ValueOrFailure())
                .ToArray();
    }
}