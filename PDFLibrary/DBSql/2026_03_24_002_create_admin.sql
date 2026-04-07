if(not exists(select * from Users where username = 'admin@gmail.com'))
begin
 INSERT [dbo].[Users] ([UserName], [PasswordHash], [FullName], [Role], [Status]) VALUES ( N'admin@gmail.com', N'81dc9bdb52d04dc20036dbd8313ed055', N'administrator', N'Admin', 1)
End