# Contributing Guide

Cảm ơn bạn đã đóng góp cho project này.
Tài liệu này mô tả các quy tắc làm việc bắt buộc khi commit code và tạo Pull Request.

*Vui lòng đọc kỹ trước khi bắt đầu.*

--------------------------------------------------

## **Workflow làm việc**
  1. **Trên Jira:** Nhận task (ví dụ SCRUM-36), chuyển trạng thái sang In Progress. [[1]](https://nhom10diem.atlassian.net/jira/software/projects/SCRUM/boards/1/backlog)
  2. **Local:** Tạo branch mới có chứa mã task. [[2]](#branching-strategy)

      Ví dụ: `git checkout -b chore/SCRUM-36-setup-env`

  3. **Code & Commit:** Code xong thì commit kèm mã task. [[3]](#commit-message-convention) Tuân thủ theo [Repository Rules](#repository-rules).

      Ví dụ: `git commit -m "SCRUM-36 chore: setup unity project and sample scene"`

  4. **Push:** Đẩy branch lên GitHub.

      Ví dụ: `git push origin chore/SCRUM-36-setup-env`

  5. **Pull Request (PR):** Tạo PR từ branch của bạn vào branch develop. [[5]](#pull-request-rules)
  
      Tiêu đề PR: Phải chứa mã task: Ví dụ `[SCRUM-36] Setup môi trường phát triển`

--------------------------------------------------

## **Branching Strategy**

Repository sử dụng mô hình branch sau:

- *main*  
  - Chỉ chứa code ổn định  
  - Không được push trực tiếp  

- *Các branch khác*

Quy ước đặt tên branch:

Cấu trúc đặt tên: `type/[JIRA-KEY]-ten-tinh-nang`
  - feature/: Tính năng mới.
  - bugfix/: Sửa lỗi.
  - chore/: Cài đặt môi trường, thư viện, cấu hình.

```
feature/[JIRA-KEY]-ten-tinh-nang
bugfix/[JIRA-KEY]-ten-loi
chore/[JIRA-KEY]-mo-ta-ngan
```
Ví dụ:
```
feature/SCRUM-40-integrate-mapbox
bugfix/SCRUM-15-fix-ui-overlap
chore/SCRUM-36-setup-env
```

--------------------------------------------------

## **Commit Message Convention**

Project sử dụng Conventional Commits.

Format bắt buộc:
```
[JIRA-KEY] <type>(<scope>): <description>
```
Để Git tự mở template này khi commit:

Chạy lệnh: `git config commit.template .gitmessage.txt`

Các type hợp lệ:
- feat     : thêm tính năng mới
- fix      : sửa bug
- docs     : tài liệu
- refactor : refactor code
- chore    : cấu hình, tool, pipeline
- test     : test

Ví dụ hợp lệ:
- SCRUM-36 feat(vr): add audience reaction system
- SCRUM-36 fix(audio): reduce microphone latency
- SCRUM-36 docs: update setup instructions
- SCRUM-36 chore: add bitbucket pipeline

Không chấp nhận:
- update
- fix bug
- abc
- Add new feature

--------------------------------------------------

## **Pull Request Rules**

**Bắt buộc:** Phải có Issue Key trong tiêu đề PR để Jira ghi nhận.
**Kiểm tra:** PR phải pass các bài test (CI pipeline) và không bị xung đột (conflict).
**Review:** Phải được ít nhất 1 thành viên khác duyệt mới được merge.

Mỗi Pull Request cần có:
- Mô tả ngắn gọn mục đích
- Danh sách thay đổi chính
- Cách test (nếu có)

Không merge nếu:
- Pipeline fail: Trường hợp sai commit hoặc branch thì chỉnh lại theo [hướng dẫn](./docs/git/FIX_COMMIT_AND_BRANCH.md)
- Push trực tiếp vào main hoặc develop

--------------------------------------------------

## **CI / CD**

Pipeline dùng để:
  - Kiểm tra build
  - Chạy test cơ bản
  - Kiểm tra format / lint (nếu có)

Pull Request không pass sẽ không được merge.

--------------------------------------------------

## **Repository Rules**

Không commit các thư mục sau:
- Library/
- Temp/
- Obj/
- Build/

Tuân thủ gitignore và gitattributes.
Không force-push lên branch chung.

--------------------------------------------------

## **Notes**

Nếu có thắc mắc về workflow hoặc rule:
- Đọc lại tài liệu này
- Hoặc liên hệ maintainer của repository

--------------------------------------------------

Bằng việc commit hoặc tạo Pull Request, bạn đồng ý tuân thủ các quy tắc trên.
