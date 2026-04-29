using System;
using UnityEngine;

/// <summary>
/// 中央イベントディスパッチャ。各システム（入力 / 状態エンジン / UI / 自律挙動 /
/// 永続化）はこの static Action を購読/発火することで、互いの存在を知らずに連携する。
///
/// 命名規約：
/// - `OnXxxRequest` … 「Xxx を実行してほしい」という要求
/// - `OnXxxChanged` … 「Xxx が変わった」という通知
/// - `OnXxxStarted` / `OnXxxReached` … フェーズ進行通知
/// </summary>
public static class GameEventHub
{
    // 状態遷移系
    public static Action OnNextStateRequest;
    public static Action OnOpenStateListRequest;
    public static Action<int> OnMotionChanged;        // 状態切替開始時（引数: 新stateIndex）

    // 速度・入力
    public static Action<float> OnSpeedChangeRequest;       // 速度変更要求
    public static Action<float> OnSpeedMultiplierChanged;   // 速度倍率変更通知
    public static Action OnSpeedResetRequest;               // 速度リセット要求

    // 視点・UI
    public static Action OnViewResetRequest;                // VR視点リセット要求
    public static Action<bool> OnToggleUI;                  // UI表示切替
    public static Action<bool, Transform> OnQuickMenuToggle;// (表示するか, どの手か)

    // ステートリスト（ハンドUIから状態選択するための機能）
    public static Action<bool, Transform> OnStateListToggle;// (開くか, どの手か)
    public static Action<int> OnStateSelect;                // 選択された状態ID
    public static Action OnGripReleased;                    // グリップが離されたことを通知

    // 入力（スティック等）
    public static Action<float> OnStickRotationRequest;     // スティック横入力での視点回転

    // 外部デバイス
    public static Action<float> OnStrokeUpdate;             // ストローク値更新（0-1）

    // ピーク状態（特殊演出用）
    public static Action<PeakTrigger> OnPeakStarted;        // ピーク開始
    public static Action OnPeakReached;                     // ピーク同期点（演出同期用）
}

/// <summary>ピーク状態を起動した契機の種別。</summary>
public enum PeakTrigger
{
    Auto,            // パラメータが閾値に達して自動で起動
    SyncedWithInput, // プレイヤー入力と連動して起動
}
