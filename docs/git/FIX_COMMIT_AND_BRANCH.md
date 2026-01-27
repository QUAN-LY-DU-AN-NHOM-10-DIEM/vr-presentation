# 🛠️ Sửa Commit Message & Đổi Tên Branch (Git)

Tài liệu này hướng dẫn:

- Cách sửa commit message khi ghi sai (không đổi code) [Phần 1](#phần-1--sửa-commit-message)
- Cách đổi tên branch cho đúng Jira key hoặc naming rule [Phần 2](#phần-2--đổi-tên-branch)
- Dành cho dev mới và dev cũ đều dùng được.


## (KHUYẾN NGHỊ): Mở commit editor bằng VS Code
- Bước 1: Mở VS Code
- Bước 2: Cấu hình Git dùng VS Code làm editor
    - Mở terminal (PowerShell / Git Bash / Terminal trong VS Code), chạy: `git config --global core.editor "code --wait"`

    📌 Ý nghĩa:

    - code → mở VS Code
    - --wait → Git chờ bạn đóng file rồi mới tiếp tục

    Chỉ cần chạy 1 lần duy nhất.

- Bước 3: Kiểm tra lại

Chạy: `git config --global core.editor`

Nếu thấy:
`code --wait`
→ OK rồi.

## PHẦN 1 – SỬA COMMIT MESSAGE
Khi nào cần sửa?
- Quên Jira key
- Ghi sai nội dung commit
- CI / workflow báo lỗi commit message

### A. Commit CHƯA push

Cách làm:

- Mở terminal
- Chạy lệnh: `git commit --amend`
- Sửa lại commit message
- Lưu và đóng editor

**➡ Code không thay đổi, chỉ đổi message.**

[Commit message đúng.](../../CONTRIBUTING.md#commit-message-convention)

### B. Commit ĐÃ push (đang mở Pull Request)

**⚠️ Chỉ force push trên branch của mình.**

Cách làm:

- Chạy: `git commit --amend`
- Sửa commit message
- Chạy: `git push --force-with-lease`

### C. Sửa commit cũ hơn (không phải commit cuối)

**⚠️ Chỉ force push trên branch của mình.**

Cách làm:

- Chạy: `git rebase -i HEAD~3` (3 = số commit gần nhất muốn xem lại)
- Tìm commit cần sửa
- Đổi chữ `pick` thành `reword`
- Lưu và đóng
- Sửa commit message khi Git yêu cầu
- Chạy: `git push --force-with-lease`

## PHẦN 2 – ĐỔI TÊN BRANCH
Khi nào cần đổi tên branch?

- Quên Jira key trong tên branch
- Đặt sai format
- Muốn rename cho đúng convention

### A. Đổi tên branch ở local

Đang đứng trên branch cần đổi tên:

Chạy: `git branch -m SCRUM-36-setup-project` ➡ Branch local đã đổi tên.

### B. Đổi tên branch đã push lên remote

Giả sử:
```
Tên cũ: setup-project
Tên mới: SCRUM-36-setup-project
```

Các bước:

- Đổi tên branch local `git branch -m SCRUM-36-setup-project`
- Push branch mới lên remote `git push origin SCRUM-36-setup-project`
- Xóa branch cũ trên remote `git push origin --delete setup-project`

*👉 Sau bước này, Pull Request có thể cần cập nhật lại branch.*

**Những điều KHÔNG nên làm**

- Không force push lên `main`
- Không đổi tên branch đang dùng chung với nhiều người
- Không sửa commit đã release