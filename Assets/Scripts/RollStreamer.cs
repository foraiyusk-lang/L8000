using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace L8000
{
    /// <summary>
    /// credits.txt を解析し、カメラの表示範囲(±マージン)に入った行だけを実体化する。
    /// 範囲外の行はプールへ返す。行数は数百なので毎フレーム全走査でも十分軽い。
    /// 生成は1フレームあたり layout.maxSpawnPerFrame 行に制限してスパイクを防ぐ。
    /// </summary>
    public class RollStreamer : MonoBehaviour
    {
        [Header("参照")]
        public GameLayout layout;
        public TMP_FontAsset font;
        public TextAsset creditsFile;
        public Camera viewCamera;
        public string groundLayerName = "Ground";

        CreditsRoll _roll;
        CreditLineBuilder[] _live;
        readonly Stack<CreditLineBuilder> _pool = new Stack<CreditLineBuilder>();
        int _groundLayer;

        public CreditsRoll Roll => _roll;
        public float TopY => _roll?.TopY ?? 0f;
        public float BottomY => _roll?.BottomY ?? 0f;

        void Awake()
        {
            if (viewCamera == null) viewCamera = Camera.main;
            _groundLayer = LayerMask.NameToLayer(groundLayerName);
            if (_groundLayer < 0) _groundLayer = 0;

            string text = creditsFile != null ? creditsFile.text : string.Empty;
            _roll = CreditsMarkup.Parse(text, layout);
            _live = new CreditLineBuilder[_roll.Lines.Count];
        }

        void LateUpdate()
        {
            if (_roll == null || _roll.Lines.Count == 0 || viewCamera == null) return;

            float halfH = viewCamera.orthographicSize;
            float camY = viewCamera.transform.position.y;
            float minY = camY - halfH - layout.streamMarginY;
            float maxY = camY + halfH + layout.streamMarginY;

            int budget = Mathf.Max(1, layout.maxSpawnPerFrame);
            var lines = _roll.Lines;

            for (int i = 0; i < lines.Count; i++)
            {
                bool inView = lines[i].WorldY >= minY && lines[i].WorldY <= maxY;
                if (inView)
                {
                    if (_live[i] == null && budget > 0)
                    {
                        _live[i] = Rent();
                        _live[i].Configure(lines[i]);
                        budget--;
                    }
                }
                else if (_live[i] != null)
                {
                    _live[i].Release();
                    _pool.Push(_live[i]);
                    _live[i] = null;
                }
            }
        }

        CreditLineBuilder Rent()
        {
            if (_pool.Count > 0)
            {
                var b = _pool.Pop();
                b.gameObject.SetActive(true);
                return b;
            }
            var go = new GameObject("Line");
            go.transform.SetParent(transform, false);
            var nb = go.AddComponent<CreditLineBuilder>();
            nb.Init(layout, font, _groundLayer);
            return nb;
        }
    }
}
