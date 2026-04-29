using UnityEngine;

/// <summary>
/// VR 管理パネルの各ページの抽象基底クラス。
/// ページの**ライフサイクルを3段階**に分けることで、表示切替時のコストを最適化する：
///
/// <list type="bullet">
/// <item><see cref="OnOpen"/>: 初回表示時に1度だけ呼ばれる。重い初期化（参照取得・リスナー登録）。</item>
/// <item><see cref="OnRefresh"/>: タブを再表示するたびに呼ばれる。値の再同期のみ。リスナー再登録はしない。</item>
/// <item><see cref="OnClose"/>: タブを閉じるたびに呼ばれる。</item>
/// </list>
///
/// このパターンにより「タブ切替で背景再生成・状態リセットが起きる」事故を防ぐ。
/// </summary>
public abstract class AdminPanelBase : MonoBehaviour
{
    /// <summary>初回表示時に1度だけ呼ばれる。重い初期化を行う。</summary>
    public virtual void OnOpen()
    {
        gameObject.SetActive(true);
    }

    /// <summary>タブを閉じる時に呼ばれる。リスナー解除など。</summary>
    public virtual void OnClose()
    {
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 既に開いていたタブを再表示する時に呼ばれる。値の同期のみ実施し、
    /// 重い初期化は行わない。<see cref="OnOpen"/> と区別することで再表示コストを抑える。
    /// </summary>
    public virtual void OnRefresh() { }
}
