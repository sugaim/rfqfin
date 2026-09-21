# Quote表示単位とManual切替時の表示クリア

## 変更理由

Traderが価格を入力する際の業務上の慣習に合わせ、Yield系列とSpread系列の単位を画面上で明示する。
また、Manualモードへ切り替えた直後に、以前のCalculated値がManual値として誤認されないようにする。

## 表示・入力単位

- Priceは従来どおり価格値として表示・入力する。
- BBG Yield、Simple Yield、Simple Yield Slide、Final Simple Yieldは`%`単位で表示・入力する。
- G-SpreadなどのSpread系列は`bp`単位で表示・入力する。
- 例えばYieldへの入力`0.8`は`0.8%`、Spreadへの入力`25`は`25 bp`を意味する。
- APIおよび永続化層では、入力された数値をこの画面単位のdecimal値として扱う。画面とAPIの間で100倍または100分の1への変換は行わない。

## Manualモード

- CalculatedからManualへ切り替えた時点では、すべての数値結果列を画面上で空欄にする。
- Manualで入力可能な数値はPriceとFinal Simple Yieldであり、それぞれ独立した値として扱う。
- Calculated payload自体は削除せず保持する。
- Calculatedへ戻した場合は、切替前のCalculated値を再表示する。

このため「Manual切替時のクリア」は表示およびManual payloadの初期化を意味し、保持済みCalculated payloadの削除は意味しない。
