Bug: Dialogue speaker name disappears / moves to the wrong side


Ở đoạn hội thoại đầu tiên, tên người nói "Mẹ" hiển thị đúng ở góc trên bên trái của dialogue box:


Name: Mẹ
Dialogue: Con ơi, mẹ đợi con lâu rồi... Vào nhà nói chuyện với mẹ đi.


Nhưng khi chuyển sang dialogue tiếp theo của cùng nhân vật Mẹ, tên "Mẹ" biến mất hoàn toàn:


Dialogue: Con không khỏe lắm phải không? Mẹ lo ...


Expected behavior:


Tên speaker "Mẹ" phải tiếp tục được hiển thị ở cùng vị trí bên trái cho tất cả các câu thoại của nhân vật Mẹ.
Không được tự động chuyển tên sang phía bên kia.
Không được để name field bị mất chỉ vì sang dialogue node/line tiếp theo.
Nếu nhiều câu liên tiếp cùng một speaker, tất cả đều phải giữ đúng speaker name và alignment.


Important: Đây có vẻ là lỗi ở state/update của speaker name hoặc dialogue layout khi chuyển line, không phải lỗi nội dung text. Hãy kiểm tra logic render/update speaker name khi dialogue index/node thay đổi.


Reproduce: Show dialogue line 1 from speaker Mẹ → advance to next dialogue line from the same speaker Mẹ → observe that the speaker name disappears / is no longer rendered on the left side.