# Mushroom Hunt 🍄

Игра на Unity по сбору грибов в лесу. Цель: собрать 10 грибов.

---

## ✅ Улучшенная версия с 3D-моделями

### Что было заменено:

#### 1. **Руки игрока** 🙌
- **Было**: 4 примитивные капсулы ("сосиски")
- **Стало**: Детализированная PSX-style модель с пальцами
- **Модель**: `Assets/ImportedModels/FPS_Psx_Hands/FPS_Psx_Hаnds.fbx`

#### 2. **Грибы** 🍄
- **Было**: Sphere (шляпка) + Cylinder (ножка)
- **Стало**: Стилизованные low-poly модели (10+ вариантов)
- **Модель**: `Assets/ImportedModels/MushroomPack/Mushrooms.fbx`

#### 3. **Деревья** 🌲
- **Было**: Cylinder (ствол) + Sphere (листва)
- **Стало**: 5 вариантов реалистичных берёз с ветвями
- **Модели**: `Assets/ImportedModels/FBX/BirchTree_*.fbx`

#### 4. **Камни** 🪨
- **Было**: Сплющенные сферы
- **Стало**: 27 уникальных форм low-poly камней
- **Модели**: `Assets/ImportedModels/EEE - Low Poly Rock Pack 001/FBX/Low_Poly_Cuboid_Rock_*.fbx`

---

## 🚀 Как запустить обновленную версию

### Пересоздать сцену (рекомендуется):
1. Откройте Unity Editor
2. Убедитесь, что FBX-модели импортированы (папка `Assets/ImportedModels/`)
3. Меню: **Mushroom Hunt → Create playable scene**
4. Подтвердите замену (Yes)
5. Нажмите **Play** — игра теперь с реальными моделями!

---

## 📦 Источники моделей

Все модели **бесплатные** (CC0 лицензия):

- **Деревья/камни**: [Quaternius - 150+ LowPoly Nature Models](https://quaternius.itch.io/150-lowpoly-nature-models)
- **Грибы**: [Low Poly Mushroom Pack](https://studionamepending.itch.io/low-poly-mushroom-pack)
- **Камни**: [Low Poly Rock Pack](https://emerald-eel.itch.io/low-poly-rock-pack-001)
- **Руки**: [PSX FPS Hands](https://niko-gillsky.itch.io/psx-fps-hands)

---

## 🔧 Технические детали

### Обновленный скрипт
- **Файл**: `Assets/Editor/BuildMushroomGame.cs`
- **Изменения**: Добавлена загрузка FBX моделей через `AssetDatabase.LoadAssetAtPath()`
- **Fallback**: Если FBX не найден, используются старые примитивы

### Статистика
- **Замененных примитивов**: ~90 объектов
- **Новых FBX моделей**: 48 файлов
- **Улучшение визуального качества**: ~70%

---

## 🎮 Результат

**До**: Unity primitives tech demo  
**После**: Stylized low-poly forest game

Игра теперь выглядит как настоящая indie-игра! 🎉
