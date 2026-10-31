using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Alterna entre as sequencias de avanco e recuo do Assaulter.
/// Cada frame e recriado em runtime com o MESMO pivot normalizado do sprite parado.
/// Isso evita que a imagem salte quando o SpriteRenderer troca entre artes com canvases diferentes.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class AssaulterMovementAnimation : MonoBehaviour
{
    const int FrameCount = 7;
    const string ResourceRoot = "Art/Characters/AssaulterMovement/";
    const byte AlphaCropThreshold = 2;

    [SerializeField, Min(1f)] float framesPerSecond = 10f;
    [SerializeField, Min(0f)] float movementThreshold = 0.0005f;

    SpriteRenderer spriteRenderer;
    Sprite idleSprite;
    readonly Sprite[] advanceFrames = new Sprite[FrameCount];
    readonly Sprite[] retreatFrames = new Sprite[FrameCount];
    readonly List<Sprite> runtimeSprites = new List<Sprite>(FrameCount * 2);

    Vector3 lastPosition;
    float frameTimer;
    int frameIndex = -1;
    int movementDirection;
    bool initialized;
    YSort ySort;
    float idleShadowWidth = 0.8f;
    float idleSpriteBoundsWidth;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    void Start()
    {
        if (spriteRenderer == null)
        {
            enabled = false;
            return;
        }

        // O sprite parado definido por MissionSetup e a referencia definitiva de escala e pivot.
        idleSprite = spriteRenderer.sprite;
        if (idleSprite == null)
        {
            enabled = false;
            return;
        }

        // Evita que o movimento procedural antigo some um segundo deslocamento vertical
        // por cima dos proprios frames da animacao.
        PixelCharacterMotion characterMotion = GetComponent<PixelCharacterMotion>();
        if (characterMotion != null) characterMotion.bobAmount = 0f;

        ySort = GetComponent<YSort>();
        if (ySort != null) idleShadowWidth = ySort.shadowWidth;
        idleSpriteBoundsWidth = idleSprite.bounds.size.x;

        LoadSequence("Advance", "advance", advanceFrames);
        LoadSequence("Retreat", "retreat", retreatFrames);
        lastPosition = transform.position;

        if (!HasFrames(advanceFrames) || !HasFrames(retreatFrames))
        {
            Debug.LogWarning("Zombit: nao foi possivel carregar os 7 frames de avanco e recuo do Assaulter. " +
                             "O personagem continuara usando o sprite estatico.", this);
            CleanupRuntimeSprites();
            enabled = false;
            return;
        }

        initialized = true;
    }

    void LateUpdate()
    {
        if (!initialized || spriteRenderer == null) return;

        Vector3 currentPosition = transform.position;
        float deltaX = currentPosition.x - lastPosition.x;
        lastPosition = currentPosition;

        if (Mathf.Abs(deltaX) <= movementThreshold)
        {
            SetIdle();
            return;
        }

        int direction = deltaX > 0f ? 1 : -1;
        Sprite[] sequence = direction > 0 ? advanceFrames : retreatFrames;

        if (direction != movementDirection)
        {
            movementDirection = direction;
            frameIndex = 0;
            frameTimer = 0f;
            ApplyFrame(sequence[frameIndex]);
            return;
        }

        float frameDuration = 1f / Mathf.Max(1f, framesPerSecond);
        frameTimer += Time.deltaTime;
        while (frameTimer >= frameDuration)
        {
            frameTimer -= frameDuration;
            frameIndex = (frameIndex + 1) % FrameCount;
        }

        ApplyFrame(sequence[frameIndex]);
    }

    void LoadSequence(string folder, string filePrefix, Sprite[] target)
    {
        // O pivot e calculado a partir do Sprite parado, nao de um valor presumido (centro/base).
        // Assim funciona mesmo que o sprite original use pivot customizado no Inspector.
        Vector2 idlePivotNormalized = new Vector2(
            idleSprite.pivot.x / Mathf.Max(1f, idleSprite.rect.width),
            idleSprite.pivot.y / Mathf.Max(1f, idleSprite.rect.height));

        for (int i = 0; i < target.Length; i++)
        {
            string path = ResourceRoot + folder + "/" + filePrefix + "_" + i.ToString("00");
            Sprite source = Resources.Load<Sprite>(path);
            if (source == null)
            {
                target[i] = null;
                continue;
            }

            target[i] = CreatePivotMatchedFrame(source, idlePivotNormalized);
        }
    }

    Sprite CreatePivotMatchedFrame(Sprite source, Vector2 normalizedPivot)
    {
        Texture2D texture = source.texture;
        Rect sourceRect = source.rect;
        Rect cropRect = sourceRect;

        // Recorta somente a transparencia horizontal. Mantemos a altura total do canvas para
        // preservar a mesma escala vertical e a mesma linha de referencia dos pes em todos os frames.
        // Os sprites estao em Resources com Read/Write ativo; se nao estiverem, usa o canvas inteiro.
        if (texture != null && texture.isReadable)
        {
            try
            {
                Color32[] pixels = texture.GetPixels32();
                int textureWidth = texture.width;
                int x0 = Mathf.RoundToInt(sourceRect.x);
                int y0 = Mathf.RoundToInt(sourceRect.y);
                int width = Mathf.RoundToInt(sourceRect.width);
                int height = Mathf.RoundToInt(sourceRect.height);
                int minX = width;
                int maxX = -1;

                for (int y = 0; y < height; y++)
                {
                    int row = (y0 + y) * textureWidth + x0;
                    for (int x = 0; x < width; x++)
                    {
                        if (pixels[row + x].a <= AlphaCropThreshold) continue;
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                    }
                }

                if (maxX >= minX)
                {
                    // Um pixel de folga evita cortar contornos semitransparentes.
                    minX = Mathf.Max(0, minX - 1);
                    maxX = Mathf.Min(width - 1, maxX + 1);
                    cropRect = new Rect(sourceRect.x + minX, sourceRect.y,
                        maxX - minX + 1, sourceRect.height);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Zombit: nao foi possivel recortar a transparencia do frame " +
                                 source.name + ": " + e.Message, this);
            }
        }

        Vector2 safePivot = new Vector2(
            Mathf.Clamp01(normalizedPivot.x),
            Mathf.Clamp01(normalizedPivot.y));

        Sprite created = Sprite.Create(texture, cropRect, safePivot, source.pixelsPerUnit,
            0, SpriteMeshType.FullRect, source.border, false);
        if (created == null) return source;

        created.name = source.name + "_AssaulterPivotMatched";
        runtimeSprites.Add(created);
        return created;
    }

    static bool HasFrames(Sprite[] frames)
    {
        if (frames == null || frames.Length != FrameCount) return false;
        for (int i = 0; i < frames.Length; i++)
            if (frames[i] == null) return false;
        return true;
    }

    void ApplyFrame(Sprite frame)
    {
        if (frame != null && spriteRenderer.sprite != frame)
            spriteRenderer.sprite = frame;

        // Mantem a sombra de contato com a mesma largura em mundo, mesmo com poses mais abertas.
        if (ySort != null && frame != null && frame.bounds.size.x > 0.01f && idleSpriteBoundsWidth > 0.01f)
            ySort.shadowWidth = idleShadowWidth * idleSpriteBoundsWidth / frame.bounds.size.x;
    }

    void SetIdle()
    {
        if (movementDirection == 0 && frameIndex < 0) return;

        movementDirection = 0;
        frameIndex = -1;
        frameTimer = 0f;
        if (idleSprite != null && spriteRenderer.sprite != idleSprite)
            spriteRenderer.sprite = idleSprite;
        if (ySort != null) ySort.shadowWidth = idleShadowWidth;
    }

    void OnDestroy()
    {
        CleanupRuntimeSprites();
    }

    void CleanupRuntimeSprites()
    {
        for (int i = 0; i < runtimeSprites.Count; i++)
        {
            if (runtimeSprites[i] == null) continue;
            if (Application.isPlaying) Destroy(runtimeSprites[i]);
            else DestroyImmediate(runtimeSprites[i]);
        }
        runtimeSprites.Clear();
    }
}
