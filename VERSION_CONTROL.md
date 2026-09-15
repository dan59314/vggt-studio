# 版本管理

目前基準版本為 `v0.1.0`，包含截至本次建立版本管理的程式功能。
先前功能開發沒有 Git 提交，因此無法回到建立版本管理之前的各個階段。

## 每次完成修改後保存版本

在此專案目錄開啟 PowerShell。先確認差異，再提交：

```powershell
git status
git diff
git add --all
git commit -m "說明這次完成的修改"
```

重要版本再加標籤，名稱不可重複，例如：

```powershell
git tag -a v0.1.1 -m "說明此版本功能"
git log --oneline --decorate -15
git tag --list
```

若未設定 Git 作者，請先填入你自己的名稱與電子郵件（只設定此儲存庫）：

```powershell
git config user.name "你的名稱"
git config user.email "你的電子郵件"
```

## 安全取回舊版本

建議將舊版本取回另一個資料夾，保留目前工作與未提交修改：

```powershell
git worktree add --detach "../VGGT-v0.1.0" v0.1.0
```

新資料夾包含該版本原始碼。以需要的標籤或提交編號替換 v0.1.0。
原資料夾仍可繼續開發；不要使用 reset --hard 作為日常回復方式。

如果要取消某次已提交的修改，又要保留完整歷史，可先提交目前工作，再執行：

```powershell
git revert 提交編號
```

若遇到衝突需處理後繼續，或以 `git revert --abort` 取消。

## 追蹤範圍

追蹤 WinForms、Python、測試程式、專案設定與文件。
不追蹤 bin/obj、Python 虛擬環境、外部 VGGT repository、模型權重、
私人影像、影片、點雲、重建輸出與驗證生成檔。
這些檔案仍保留在原位置，Git 回復不會還原它們。

舊版原始碼可重新編譯：

```powershell
dotnet build VGGT.WinForms/VGGT.WinForms.csproj -c Release
```

另一個 worktree 執行時，需要在「執行環境／輸出位置」指定既有 Python
與 VGGT repository 的完整路徑。部分驗證需要另外準備影像／推估快取。
使用者配置存於 LocalAppData/VGGT Studio，亦不屬於原始碼版本管理。

目前僅有本機 Git 儲存庫；這能回復原始碼版本，但不是異機備份。
之後可另行設定私人遠端儲存庫。
