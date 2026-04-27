# Configurators

[English](README.md) | **Русский**

---

Сериализуемая, пулящаяся и DI-aware система для **модификаций** (одноразовых эффектов, применяемых к контексту), **инструкций** (одноразовых эффектов без контекста, с инспекторными ссылками на цели), **условий** (булевых предикатов с уведомлениями об изменениях) и **экстеншенов** (пассивных носителей значений, читаемых по запросу). Всё это строится из полиморфных `[SerializeReference]`-списков, которые ты заполняешь в инспекторе.

## Установка

Один из двух вариантов на выбор.

**1. `.unitypackage` (рекомендуется).** Скачай последний `Configurators.unitypackage` со страницы [Releases](../../releases) и либо дважды кликни по файлу при открытом Unity, либо в меню `Assets → Import Package → Custom Package...`. Unity покажет список файлов — оставь всё отмеченным и подтверди.

**2. Ручное копирование.** Скачай репозиторий (Code → Download ZIP, либо `git clone`) и положи папку `Configurators` куда угодно внутрь `Assets/` своего проекта.

В обоих случаях всё компилируется в стандартный `Assembly-CSharp` — никаких asmdef и манифестов.

**Требования:** Unity 6000.0 или новее, [Zenject / Extenject](https://github.com/Mathijs-Bakker/Extenject) в проекте.

## Концепции

| Термин | Что это                                                                                                                                                |
|---|--------------------------------------------------------------------------------------------------------------------------------------------------------|
| **Modification** | Единица работы, применяемая к некоторому `TContext`. Например: «выставить max HP», «добавить тег», «заспавнить дочерний объект».                       |
| **Instruction** | Единица работы, выполняемая без контекста — цели хранятся прямо в инспекторных полях data-класса. Например: `GameObjectSetActive`, `PlaySound`.        |
| **Condition** | Булево условие, которое можно проверить (`IsMet`) и слушать (`AddListener`).                                                                           |
| **Extension** | Пассивный носитель значения, прикреплённый к конфигу (например, `MaxCount`, `Cooldown`). Читается фичей через `ExtensionProcessor.TryGetExtension<T>`. |
| **Processor** | Контейнер со списком модификаций / инструкций / условий / экстеншенов. Лежит на конфиге или компоненте.                                                |
| **Handler** | Резолвится через DI и пулится. Опционален — нужен только если хочешь инжект сервисов или повторное использование.                                      |
| **ConfiguratorManager** | Project-scoped сервис, который резолвит хендлеры, управляет их лайфтаймом и подключает условия к слушателям.                                           |

## Определение модификации

Два варианта.[^actor-unit]

### Inline (без DI и пулинга)

Для простых модификаций без внешних зависимостей. Логика лежит прямо на data-классе.

```csharp
[Serializable]
[ConfiguratorCategory("Stats")]
public class SetMaxHealth : Modification<ActorUnit>
{
    public int Value;

    public override void Apply(ActorUnit context)
    {
        context.GetAbility<HealthAbility>().SetMax(Value);
    }
}
```

### С хендлером (DI + пулинг)

Для модификаций, которым нужны инжектируемые сервисы или состояние. Данные и логика — в разных классах.

```csharp
// данные — лежат в конфиге
[Serializable]
[ConfiguratorCategory("Spawn")]
public class SpawnChild : ModificationData<ActorUnit, SpawnChildHandler>
{
    public ActorUnit Prefab;
    public Vector2 Offset;
}

// хендлер — создаётся Zenject-ом, возвращается в пул при диспозе
public class SpawnChildHandler : ModificationHandler<SpawnChild, ActorUnit>
{
    [Inject] private readonly IObjectCreator _creator;
    
    public override void Apply(ActorUnit context)
    {
        _creator.Instantiate(Data.Prefab, (Vector2)context.transform.position + Data.Offset, null);
    }
}
```

## Определение инструкции

Инструкция — это модификация без контекста: цель хранится прямо в сериализованных полях data-класса, а `Apply()` вызывается без аргументов. Те же два варианта.

### Inline (без DI и пулинга)

```csharp
[Serializable]
[ConfiguratorCategory("GameObject")]
public class GameObjectSetActive : Instruction
{
    public GameObject Object;
    public bool Value;

    public override void Apply() => Object.SetActive(Value);
}
```

### С хендлером (DI + пулинг)

```csharp
// данные
[Serializable]
[ConfiguratorCategory("Audio")]
public class PlaySound : InstructionData<PlaySoundHandler>
{
    public AudioClip Clip;
    [Range(0, 1)] public float Volume = 1f;
}

// хендлер — создаётся Zenject-ом, возвращается в пул при диспозе
public class PlaySoundHandler : InstructionHandler<PlaySound>
{
    [Inject] private readonly IAudioService _audio;

    public override void Apply() => _audio.PlayOneShot(Data.Clip, Data.Volume);
}
```

## Определение условия

Те же два варианта.

### Inline

```csharp
[Serializable]
[ConfiguratorCategory("Time")]
public class IsNight : Condition
{
    public override bool IsMet() => DayCycle.Current == TimeOfDay.Night;
    // вызывай NotifyChanged() когда внутреннее состояние меняется
}
```

### С хендлером

```csharp
[Serializable]
public class HealthBelow : ConditionData<HealthBelowHandler>
{
    [Range(0, 1)] public float Threshold;
}

public class HealthBelowHandler : ConditionHandler<HealthBelow>
{
    [Inject] private readonly IHealthService _health;

    public override bool IsMet() => _health.Ratio < Data.Threshold;

    protected override void OnFirstListenerAdded() => _health.OnChanged += NotifyChanged;
    protected override void OnLastListenerRemoved() => _health.OnChanged -= NotifyChanged;
}
```

## Композитные условия

Комбинируй условия через `All`, `Any`, `None`, `Not` — это обычные условия, поэтому они вкладываются друг в друга:

```
All
├── HealthBelow (0.5)
├── IsNight
└── Not
    └── HasItem (key)
```

Слушатели на композите срабатывают **один раз на каждое изменение внутри**, независимо от количества внешних подписчиков.

## Экстеншены

Чистые данные, прикреплённые к конфигу и читающиеся по запросу. Без хендлера и резолва.

```csharp
[Serializable]
[ConfiguratorCategory("Limits")]
public class MaxCount : Extension<int>
{
    [SerializeField] private int _value;
    
    public override int Value => _value;
}

// использование — implicit-преобразование в T поддерживается
int max = item.ExtensionProcessor.TryGetExtension(out MaxCount ext) ? ext : int.MaxValue;
```

Если экстеншенов одного типа несколько — используй `GetExtensions<T>()`.

## Рантайм-использование

Инжекти `IConfiguratorManager` и вызывай один из методов ниже.

### Применить модификации к контексту

```csharp
[Inject] private readonly IConfiguratorManager _configurators;

// С Component-владельцем — авто-диспоз при уничтожении его GameObject'а.
private void Spawn(ActorUnit actor)
{
    _configurators.ApplyModifications(actor.Modifications, actor, lifetimeOwner: actor);
}

// Без владельца — вызывающий сам управляет диспозом.
// Полезно в сервисах, где нет MonoBehaviour.
public class SomeService : IDisposable
{
    [Inject] private readonly IConfiguratorManager _configurators;
    
    private IDisposable _binding;

    public void Setup(ModificationProcessor<SomeContext> processor, SomeContext context)
    {
        _binding = _configurators.ApplyModifications(processor, context);
    }

    public void Dispose() => _binding?.Dispose();
}
```

### Применить инструкции

То же самое, что и для модификаций, только без аргумента-контекста:

```csharp
[Inject] private readonly IConfiguratorManager _configurators;

[SerializeField] private InstructionProcessor onSpawn;

private void Start()
{
    _configurators.ApplyInstructions(onSpawn, lifetimeOwner: this);
}
```

### Подписаться на процессор условий

```csharp
private IDisposable _sub;

private void OnEnable()
{
    _sub = _configurators.SubscribeConditions(
        config.Conditions,
        isMet => gameObject.SetActive(isMet),
        lifetimeOwner: this);
}

// _sub.Dispose() — чтобы отписаться раньше; иначе авто-диспоз при уничтожении `this`.
```

### Только резолв (низкий уровень)

Когда нужно отделить момент привязки хендлеров от момента применения модификаций / инструкций / условий:

```csharp
IDisposable binding = _configurators.ResolveModifications(processor);
processor.Apply(context);   // можно вызывать несколько раз
processor.Apply(otherContext);
// ... позже ...
binding.Dispose();
```

## Инспектор

Встроенные процессоры (`ModificationProcessor<T>`, `InstructionProcessor`, `ConditionProcessor`, `ExtensionProcessor`) уже отдают типизированный дропдаун — кладёшь процессор в конфиг / компонент и оно работает, никаких списков объявлять не надо.

Чтобы сгруппировать свои модификации / инструкции / условия / экстеншены под подменю в этом дропдауне, навесь на класс `[ConfiguratorCategory("Path/Submenu")]`:

```csharp
[Serializable]
[ConfiguratorCategory("Inventory/Item")]
public class MaxCount : Extension<int> { ... }
```

<p align="center">
  <img src="Documentation~/inspector.gif" alt="Добавление Extension через типизированный дропдаун" width="520">
</p>

Если зачем-то понадобится полиморфный список вне встроенных процессоров — навесь на него сам `[SerializeReference, ConfiguratorSelector]`, это тот же атрибут, что используется процессорами внутри.

## Контракт лайфтайма

* `ApplyModifications` / `ApplyInstructions` / `SubscribeConditions` всегда возвращают `IDisposable`. С `lifetimeOwner` cleanup автоматический на уничтожении владельца; без него — вызывающий сам отвечает за диспоз.
* Вызов одного и того же метода дважды на одном процессоре без диспоза первого — баг, выдаёт warning и no-op.
* `Dispose()` идемпотентен и безопасен в любом порядке — исключения в cleanup логируются, а не пробрасываются.

## Лицензия

Распространяется под [MIT License](LICENSE.md). Свободно для использования в личных и коммерческих проектах.

Автор — **Egor Shesterikov**.

[^actor-unit]: `ActorUnit` во всех примерах — это кастомный класс самого проекта, не часть модуля. Подставь любой свой тип, над которым нужно применять модификации или проверять условия. Модификации и условия не привязаны к конкретному `TContext`.
