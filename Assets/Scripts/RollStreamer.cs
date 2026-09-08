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
        public Transform player;   // カメラ外に落ちてもプレイヤー周辺の足場は残す
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

            // プレイヤー周辺(画面外に落ちても足場が消えないように)
            float pMin = float.MaxValue, pMax = float.MinValue;
            if (player != null)
            {
                float py = player.position.y;
                pMin = py - 8f;
                pMax = py + 8f;
            }

            int budget = Mathf.Max(1, layout.maxSpawnPerFrame);
            var lines = _roll.Lines;

            for (int i = 0; i < lines.Count; i++)
            {
                float y = lines[i].WorldY;
                bool inView = (y >= minY && y <= maxY) || (y >= pMin && y <= pMax);
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

        /// <summary>実体化済みの行を全て組み直す(降り演出で当たり判定ルールが変わったとき等)。</summary>
        public void RefreshAll()
        {
            if (_roll == null || _live == null) return;
            for (int i = 0; i < _live.Length; i++)
                if (_live[i] != null) _live[i].Configure(_roll.Lines[i]);
        }

        /// <summary>指定 Y 範囲の行を今すぐ(予算無視で)実体化する。スポーン直後の落下抜けを防ぐため。</summary>
        public void SpawnImmediate(float minY, float maxY)
        {
            if (_roll == null) return;
            var lines = _roll.Lines;
            for (int i = 0; i < lines.Count; i++)
            {
                if (_live[i] != null) continue;
                float y = lines[i].WorldY;
                if (y < minY || y > maxY) continue;
                _live[i] = Rent();
                _live[i].Configure(lines[i]);
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
