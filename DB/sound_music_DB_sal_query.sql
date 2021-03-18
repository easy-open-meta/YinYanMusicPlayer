if DB_ID('sound_music_DB') is not null
   drop database sound_music_DB
go
create database sound_music_DB
on
(
	name = 'sound_music_DB',
	filename = 'E:\罗良健\音言音乐播放器\WordMusicWinfrom\DB\sound_music_DB.mdf'
)

use sound_music_DB




if OBJECT_ID ('user_info') is not null
drop table user_info
go

--用户信息表
create table user_info
(
	u_id int identity(10000001,1) unique,--用户ID
	u_full_name varchar(18) ,--用户账号 用于登录自动生成
	u_pwd varchar(18),--用户密码
	u_name varchar(18) ,--用户名字
	register_time date,--创造时间
	u_birthday date ,--用户生日
	u_sex varchar(4) check(u_sex in('保密','男','女')) default '保密',--性别
	u_image varchar(100),--用户默认头像 可修改
	u_signature varchar(80),--用户签名	
)	
select * from user_info

--select u_full_name from user_info where u_full_name = '用户输入账号'
select * from user_info

--ID、用户名、出生日期、创建日期、性别、头像图片地址、签名

insert user_info values ('10000001','123456','洛音','2016-12-24','2000-11-24','男','C:\Users\admin\Desktop\SKY小组项目内容\游栈Logo.png','我是只是第一个测试员')
insert user_info values ('10000002','123456','七音','2016-06-24','2000-10-24','男','C:\Users\admin\Desktop\SKY小组项目内容\游栈Logo.png','我是只是第二个测试员')
insert user_info values ('10000003','123456','桑音','2016-10-24','2000-09-24','男','C:\Users\admin\Desktop\SKY小组项目内容\游栈Logo.png','我是只是第三个测试员')
insert user_info values ('10000004','123456','殇音','2016-06-24','2000-08-24','女','C:\Users\admin\Desktop\SKY小组项目内容\游栈Logo.png','我是只是第四个测试员')
insert user_info values ('10000005','123456','诺音','2016-10-24','2000-07-24','女','C:\Users\admin\Desktop\SKY小组项目内容\游栈Logo.png','我是只是第五个测试员')
insert user_info values ('10000006','123456','白音','2016-06-24','2000-06-24','保密','C:\Users\admin\Desktop\SKY小组项目内容\游栈Logo.png','我是只是第六个测试员')
insert user_info values ('10000007','123456','其音','2016-10-24','2000-05-24','保密','C:\Users\admin\Desktop\SKY小组项目内容\游栈Logo.png','我是只是第七个测试员')
insert user_info values ('10000008','123456','憨音','2016-06-24','2000-04-24','保密','C:\Users\admin\Desktop\SKY小组项目内容\游栈Logo.png','我是只是第八个测试员')
insert user_info values ('10000009','123456','杰音','2016-10-24','2000-04-24','保密','C:\Users\admin\Desktop\SKY小组项目内容\游栈Logo.png','我是只是第九个测试员')
insert user_info values ('10000010','123456','洲音','2016-10-24','2000-03-24','保密','C:\Users\admin\Desktop\SKY小组项目内容\游栈Logo.png','我是只是第十个测试员')

insert user_info values ('10000011','123456','音','2016-10-24','2000-03-24','保密','C:\Users\admin\Desktop\SKY小组项目内容\游栈Logo.png','我是只是第十个测试员')

delete from user_info

if OBJECT_ID ('user_like_music') is not null
drop table user_like_music
go

--用户喜好音乐表
create table user_like_music    
(
	u_id varchar(8)	not null,
	par_name varchar(18) not null,
	par_id varchar(8) not null,
	s_name varchar(18) not null,
	s_id varchar(8) not null,
)





if OBJECT_ID ('user_song_list') is not null
drop table user_song_list
go

--用户歌单表
create table user_song_list
(
	u_id varchar(8),
	u_name varchar(100),
	
	par_id varchar(100),
	
	song_id int identity(10000001,1),
	song_name varchar(30) ,
	song_collection int ,
	song_sing_u int ,
	song_info varchar(120),
	song_time datetime,
	song_pic varchar(110),
)

update user_song_list set song_collection = 23 where song_id = 10000006 


select * from user_song_list where u_id = 10000001
select Count(*) from user_song_list where u_id = 10000001

select * from user_collection_song_sheet where user_id = 10000001

select a_id,cd_name,m_singer,a_name,song_time,par_lyric,music_address,cd_id from song_music where song_id = 10000001

select u_id,u_name,par_id,b.song_id,song_name,song_collection,song_sing_u,song_info,song_time,song_pic from user_collection_song_sheet a left join user_song_list b on a.song_id = b.song_id where a.user_id = 10000001

delete from user_song_list where song_id = 10000001

delete from user_song_list where song_sing_u = 1

select * from user_song_list where par_id='1001'

--update user_song_list set song_collection = '"+song_collection+"' and song_id = '"+song_id+"'

insert user_song_list values('10000001','洛音','1002','【古风】风韵犹存古来香','8312','12','喜欢古风的小伙伴们记得收藏一下哟','2019-02-01','SongSheetIMG\32.png')
insert user_song_list values('10000001','洛音','1002','【现代】钢筋森林','999','8','我本卑微，却君临天下','2019-03-04','SongSheetIMG\24.png')
insert user_song_list values('10000001','洛音','1002','余生有你，足矣','695','0','余生无所求，若有你伴我左右，同我共赏天地繁华，共食这人间烟火，我便是死也无憾。','2019-06-21','SongSheetIMG\22.png')
insert user_song_list values('10000001','洛音','1002','心悦君兮君不知','352','0','山有木兮木有枝，心悦君兮君不知','2019-06-21','SongSheetIMG\18.png')
insert user_song_list values('10000002','七音','1001','jay杰伦','106','0','','2016-10-10','SongSheetIMG\16.png')
insert user_song_list values('10000002','七音','1002','老大最帅了老大最帅了','22','0','','2019-10-11','SongSheetIMG\11.png')
insert user_song_list values('10000002','七音','1001','七七','16','0','','2019-11-11','SongSheetIMG\8.png')
insert user_song_list values('10000001','洛音','1005','泪','15','0','','2019-11-12','SongSheetIMG\27.png')
insert user_song_list values('10000001','洛音','1006','纯音乐 解压 看书','10','0','','2019-01-12','SongSheetIMG\20.png')
insert user_song_list values('10000001','洛音','1005','不留遗憾|错过了你','4','0','','2019-11-12','SongSheetIMG\31.png')

insert user_song_list values('10000001','洛音','1003','咚鼓|DJ合集','2','0','','2019-11-12','SongSheetIMG\1.png')
insert user_song_list values('10000003','桑音','1001','NBA篮球大使音乐合集','1','0','','2019-11-12','SongSheetIMG\2.png')
insert user_song_list values('10000003','桑音','1001','【坤坤】妈妈歌单','1','0','','2019-11-12','SongSheetIMG\3.png')
insert user_song_list values('10000003','桑音','1001','【鹿晗】妈妈爱你','1','0','','2019-11-12','SongSheetIMG\4.png')
insert user_song_list values('10000003','桑音','1004','自古污歌封面美','1','0','','2019-11-12','SongSheetIMG\5.png')
insert user_song_list values('10000003','桑音','1009','蒸汽波——放松','0','0','','2019-11-12','SongSheetIMG\6.png')
insert user_song_list values('10000003','桑音','1009','蒸汽波——复古','0','0','','2019-11-12','SongSheetIMG\7.png')
insert user_song_list values('10000001','洛音','1001','这样的男声我可以','0','0','','2019-11-12','SongSheetIMG\12.png')
insert user_song_list values('10000001','洛音','1003','来，把嘴张开','0','0','','2019-11-12','SongSheetIMG\9.png')
insert user_song_list values('10000001','洛音','1008','二次元|萝莉合集','0','0','','2019-11-12','SongSheetIMG\10.png')

insert user_song_list values('10000001','洛音','1001','无与伦比|杰伦','0','0','','2019-11-12','SongSheetIMG\13.png')
insert user_song_list values('10000006','白音','1005','我还爱你，只是你已不再','0','0','','2019-11-12','SongSheetIMG\14.png')
insert user_song_list values('10000006','白音','1001','窗外的麻雀还在电线杆上多嘴','0','0','','2019-11-12','SongSheetIMG\15.png')
insert user_song_list values('10000006','白音','1005','睡不着的时候都在想你','0','0','','2019-11-12','SongSheetIMG\17.png')
insert user_song_list values('10000006','白音','1005','腐、腐、腐','0','0','','2019-11-12','SongSheetIMG\19.png')
insert user_song_list values('10000006','白音','1002','台下人走过不见旧颜色','0','0','','2019-11-12','SongSheetIMG\21.png')
insert user_song_list values('10000006','白音','1001','男友力Max|小奶狗','0','0','','2019-11-12','SongSheetIMG\23.png')
insert user_song_list values('10000006','白音','1006','电台主持、小莫','0','0','','2019-11-12','SongSheetIMG\25.png')
insert user_song_list values('10000001','洛音','1006','你的身边，他还在吗?','0','0','','2019-11-12','SongSheetIMG\28.png')
insert user_song_list values('10000004','殇音','1004','只做你的小猫咪|喵','0','0','','2019-11-12','SongSheetIMG\29.png')

insert user_song_list values('10000004','殇音','1009','不谈恋爱，逼事没有','0','0','','2019-11-12','SongSheetIMG\30.png')
insert user_song_list values('10000004','殇音','1008','大橘已定|橘色','0','0','','2019-11-12','SongSheetIMG\33.png')
insert user_song_list values('10000004','殇音','1001','AWSL|韩','0','0','','2019-11-12','SongSheetIMG\34.png')
insert user_song_list values('10000005','诺音','1001','陈一发儿|麻麻','0','0','','2019-11-12','SongSheetIMG\35.png')
insert user_song_list values('10000005','诺音','1009','莓良心','0','0','','2019-11-12','SongSheetIMG\36.png')
insert user_song_list values('10000005','诺音','1009','海星1号','0','0','','2019-11-12','SongSheetIMG\37.png')
insert user_song_list values('10000007','其音','1009','海星2号','0','0','','2019-11-12','SongSheetIMG\38.png')
insert user_song_list values('10000008','憨音','1009','海星3号','0','0','','2019-11-12','SongSheetIMG\39.png')
insert user_song_list values('10000009','杰音','1001','八爷|挚爱','0','0','','2019-11-12','SongSheetIMG\40.png')
insert user_song_list values('10000010','洲音','1001','小天使','0','0','','2019-11-12','SongSheetIMG\26.png')


insert user_song_list values('用户ID','用户名字','歌单类型','歌单名字','0','0','','2019-11-12','SongSheetIMG\26.png')


select *from user_song_list

select u_id,u_name,par_id,b.song_id,song_name,song_collection,song_sing_u,song_info,song_time,song_pic from user_collection_song_sheet a left join user_song_list b on a.song_id = b.song_id where a.user_id = 10000001

select * from user_collection_song_sheet a left join user_song_list b on a.song_id = b.song_id where a.user_id = 10000001
delete from user_collection_song_sheet where song_id = 10000002

--用户关注歌单表
if OBJECT_ID ('user_collection_song_sheet') is not null
drop table user_collection_song_sheet
go
create table user_collection_song_sheet
(
	--用户的ID
	user_id varchar(8) not null,
	--歌单的ID
	song_id varchar(8) not null,	
)
--查看你的收藏歌单
select * from user_collection_song_sheet where user_id = 10000001
--查看这个歌单被几个人收藏了
select * from user_collection_song_sheet where song_id = 10000001

select Count(*) from user_collection_song_sheet where user_id = '"+user_id+"' and song_id = '"+song_id+"'

insert user_collection_song_sheet values (10000001,10000005)
insert user_collection_song_sheet values (10000001,10000006)
insert user_collection_song_sheet values (10000001,10000007)
insert user_collection_song_sheet values (10000001,10000012)
insert user_collection_song_sheet values (10000001,10000013)
insert user_collection_song_sheet values (10000001,10000014)
insert user_collection_song_sheet values (10000001,10000015)
insert user_collection_song_sheet values (10000001,10000016)
insert user_collection_song_sheet values (10000001,10000017)

insert user_collection_song_sheet values (10000002,10000001)
insert user_collection_song_sheet values (10000003,10000001)
insert user_collection_song_sheet values (10000004,10000001)
insert user_collection_song_sheet values (10000005,10000001)
insert user_collection_song_sheet values (10000006,10000001)
insert user_collection_song_sheet values (10000007,10000001)
insert user_collection_song_sheet values (10000008,10000001)
insert user_collection_song_sheet values (10000009,10000001)
insert user_collection_song_sheet values (10000010,10000001)

insert user_collection_song_sheet values (10000002,10000002)
insert user_collection_song_sheet values (10000003,10000002)
insert user_collection_song_sheet values (10000004,10000002)
insert user_collection_song_sheet values (10000005,10000002)
insert user_collection_song_sheet values (10000006,10000002)
insert user_collection_song_sheet values (10000007,10000002)
insert user_collection_song_sheet values (10000008,10000002)
insert user_collection_song_sheet values (10000009,10000002)



--用户关注表And用户粉丝表
if OBJECT_ID ('user_followandfans') is not null
drop table user_followandfans
go
create table user_followandfans
(
	--被关注用户的ID
	fuser_id varchar(8) not null,
	--关注用户的ID
	user_id varchar(8) not null,
	
)
--查看自己关注数
select * from user_followandfans where user_id = 10000001
--查看粉丝数
select * from user_followandfans where fuser_id = 10000001

insert user_followandfans values (10000001,10000002)
insert user_followandfans values (10000001,10000003)
insert user_followandfans values (10000001,10000004)
insert user_followandfans values (10000001,10000005)
insert user_followandfans values (10000001,10000006)
insert user_followandfans values (10000001,10000007)
insert user_followandfans values (10000001,10000008)
insert user_followandfans values (10000001,10000009)
insert user_followandfans values (10000001,10000010)

insert user_followandfans values (10000002,10000001)
insert user_followandfans values (10000002,10000005)
insert user_followandfans values (10000002,10000006)
insert user_followandfans values (10000002,10000007)
insert user_followandfans values (10000002,10000008)
insert user_followandfans values (10000002,10000009)
insert user_followandfans values (10000002,10000010)







if OBJECT_ID ('song_music') is not null
drop table song_music
go
--歌单音乐表
--通过专辑的ID和歌曲的专辑内ID可以快速的找到专辑中的单曲音乐，并载入歌单。
--select * from 表名 where a_id = 专辑ID and cd_id = 专辑内单曲ID
create table song_music
(
	song_id varchar(8) not null,--歌单的ID 
	songone_id varchar(8) not null,--歌单内单曲的ID
	a_id varchar(8) not null,--专辑的ID
	a_name varchar(18) not null,--专辑的名字
	m_singer varchar(18) not null,--专辑的歌手
	par_lyric varchar(100) not null,--歌单歌词
	music_address varchar(100) not null,--歌单歌曲
	song_time varchar(12) not null,--歌单时间
	cd_id varchar(8) not null,--专辑内ID
	cd_name varchar(100) not null,--歌曲名字

)
--查询当前播放的歌曲
select * from song_music where song_id = 10000001 and songone_id = 1
select * from song_music where song_id = 10000001

select Count(*) from song_music where song_id = 10000001

select * from song_music 

delete from song_music where song_id = 10000001

select * from upload_music where a_id = (select top 1 a_id from song_music where song_id = 10000001) and cd_id = (select top 1 cd_id from song_music where song_id = 10000001)
--insert song_music values('歌单的ID','歌单内单曲的ID','专辑的ID',专辑名字,专辑歌手名字,歌词位置,音乐位置,播放时间,'专辑内歌曲的ID','歌曲名字')
select a_id,cd_name,m_singer,a_name,song_time,par_lyric,music_address,cd_id from song_music where song_id = 10000001
insert song_music values ('10000001','1','10000001','青年报社','许嵩','SingSonglrc\许嵩 - 雅俗共赏.lrc','SingSong\许嵩 - 雅俗共赏.flac','04:09','1','雅俗共赏')
insert song_music values ('10000001','2','10000001','青年报社','许嵩','SingSonglrc\许嵩 - 老古董.lrc','SingSong\许嵩 - 老古董.flac','04:04','2','老古董')
insert song_music values ('10000001','3','10000001','青年报社','许嵩','SingSonglrc\许嵩 - 有桃花.lrc','SingSong\许嵩 - 有桃花.flac','04:02','3','有桃花')
insert song_music values ('10000001','4','10000001','青年报社','许嵩','SingSonglrc\许嵩 - 等到烟火清凉.lrc','SingSong\许嵩 - 等到烟火清凉.flac','03:01','4','等到烟火清凉')
insert song_music values ('10000001','5','10000001','青年报社','许嵩','SingSonglrc\许嵩 - 弹指一挥间.lrc','SingSong\许嵩 - 弹指一挥间.flac','04:47','5','弹指一挥间')
insert song_music values ('10000001','6','10000001','青年报社','许嵩','SingSonglrc\许嵩 - 今年勇.lrc','SingSong\许嵩 - 今年勇.flac','03:24','6','今年勇')
insert song_music values ('10000001','7','10000001','青年报社','许嵩','SingSonglrc\许嵩 - 七夕.lrc','SingSong\许嵩 - 七夕.flac','03:53','7','七夕')
insert song_music values ('10000001','8','10000001','青年报社','许嵩','SingSonglrc\许嵩 - 山水之间.lrc','SingSong\许嵩 - 山水之间.flac','04:36','8','山水之间')
insert song_music values ('10000001','9','10000001','青年报社','许嵩','SingSonglrc\许嵩 - 梧桐灯.lrc','SingSong\许嵩 - 梧桐灯.flac','04:37','9','梧桐灯')
insert song_music values ('10000001','10','10000001','青年报社','许嵩','SingSonglrc\许嵩 - 隐隐约约.lrc','SingSong\许嵩 - 隐隐约约.flac','03:23','10','隐隐约约')
insert song_music values ('10000001','11','10000002','不如吃茶去','许嵩','SingSonglrc\许嵩 - 宇宙之大.lrc','SingSong\许嵩 - 宇宙之大.flac','04:33','1','宇宙之大')
insert song_music values ('10000001','12','10000003','雨幕','许嵩','SingSonglrc\许嵩 - 雨幕.lrc','SingSong\许嵩 - 雨幕.flac','04:00','1','雨幕')

insert song_music values ('10000002','1','20000001','怪咖','薛之谦','SingSonglrc\薛之谦 - 怪咖.lrc','SingSong\薛之谦 - 怪咖.flac','04:10','1','怪咖')
insert song_music values ('10000002','2','20000001','怪咖','薛之谦','SingSonglrc\薛之谦 - 狐狸.lrc','SingSong\薛之谦 - 狐狸.flac','03:54','2','狐狸')
insert song_music values ('10000002','3','20000001','怪咖','薛之谦','SingSonglrc\薛之谦 - 摩天大楼.lrc','SingSong\薛之谦 - 摩天大楼.flac','03:50','3','摩天大楼')
insert song_music values ('10000002','4','20000001','怪咖','薛之谦','SingSonglrc\薛之谦 - 那是你离开了北京的生活.lrc','SingSong\薛之谦 - 那是你离开了北京的生活.flac','04:28','4','那是你离开了北京的生活')
insert song_music values ('10000002','5','20000001','怪咖','薛之谦','SingSonglrc\薛之谦 - 天份.lrc','SingSong\薛之谦 - 天份.flac','04:08','5','天份')
insert song_music values ('10000002','6','20000001','怪咖','薛之谦','SingSonglrc\薛之谦 - 哑巴.lrc','SingSong\薛之谦 - 哑巴.flac','04:21','6','哑巴')
insert song_music values ('10000002','7','20000001','怪咖','薛之谦','SingSonglrc\薛之谦 - 最好.lrc','SingSong\薛之谦 - 最好.flac','04:15','7','最好')
insert song_music values ('10000002','8','20000001','怪咖','薛之谦','SingSonglrc\薛之谦 - 肆无忌惮.lrc','SingSong\薛之谦 - 肆无忌惮.flac','04:19','8','肆无忌惮')








if OBJECT_ID ('music') is not null
drop table music
go
--音乐表
create table music
(
	cd_id varchar(8) not null unique,
	cd_name varchar(18) not null,
	cd_album varchar(18) not null,
	cd_singer varchar(18) not null,
	cd_lyric varchar(1000) not null,
	cd_link varchar(100) not null,
)

if OBJECT_ID ('album') is not null
drop table album
go
--专辑表 有哪些专辑 分别是谁做的专辑
create table album
(
	s_id varchar(8) not null,
	s_name varchar(18) not null,
	a_id  varchar(8) not null,
	a_name varchar(18) not null,
	a_time varchar(18) not null,
	a_details varchar(500) not null,
	a_bigpic varchar(100) not null,
	a_smlpic varchar(100) not null,
	a_smlpictx varchar(100) not null,
)
select * from album
 
 select a_smlpictx from album where a_id =10000001
 
select * from album where s_name = '许嵩';

insert album values('s_100001','许嵩','10000001','青年报社','2012-12-24','《青年晚报》，是一个三十而立的青年敞开自己和你面对面交心，也是一家晚报记述着生活的真相和所引发的思考，感性与理性交叠。歌词读来有如摊开一张晚报所看到的版面：社会生活、人生况味、暖心故事、摄影专栏，甚至不缺关于“最佳歌手”的娱乐头条以及“早睡身体好” 的养生大法','SingSongpic\SSP10001.png','SingSongpic\SSP20001.png','SingSongpic\SMP10001.png')
insert album values('s_100001','许嵩','10000002','不如吃茶去','2012-12-24','关于爱恨情仇的道理，关于人生的感悟，我们说了那么多，也听了那么多，最后又如何？','SingSongpic\SSP10002.png','SingSongpic\SSP20002.png','SingSongpic\SMP10002.png')
insert album values('s_100001','许嵩','10000003','雨幕','2012-12-24','时隔一年未见新作，此次“飘然一曲”诱得广大乐迷“侧耳听”。潇潇的雨幕里，三日无言的福船上，饮酒、听曲、赏雨、忆人，然而故事却在有人卧于绯泊之中时戛然而止，故事表象下的诸多隐喻也浮出水面。','SingSongpic\SSP10003.png','SingSongpic\SSP20003.png','SingSongpic\SMP10003.png')
insert album values('s_100002','薛之谦','20000001','怪咖','2012-11-24','何为怪咖？是霓虹幻影后面嬉笑怒骂的群像？是风平浪静里面歇斯里地的疯狂？不是平庸的平庸，不是普通的普通，怪咖不怪，只是被人们理解为意义上的“怪”','SingSongpic\SSP10004.png','SingSongpic\SSP20004.png','SingSongpic\SMP10004.png')
insert album values('s_100002','薛之谦','20000002','绅士','2012-11-24','当那背影离去 仿佛也可以 脱下礼帽 深情致敬全唱作把『心』交出来最认真的薛之谦','SingSongpic\SSP10005.png','SingSongpic\SSP20005.png','SingSongpic\SMP10005.png')
insert album values('s_100002','薛之谦','20000003','渡','2012-11-24','2012年华语音乐圈最受期待唱片作品全能唱作奇才薛之谦掏心力作 昨日渡往明日从回忆渡往梦想从现实渡往幻境','SingSongpic\SSP10006.png','SingSongpic\SMP10001.png','SingSongpic\SMP10006.png')
insert album values('s_100002','薛之谦','20000004','一半','2012-11-24','一半的我，不完整，一半的人生，差一半。一半的我用心的唱故事给你听，一半的你是否能够感知到？','SingSongpic\SSP10007.png','SingSongpic\SSP20007.png','SingSongpic\SMP10007.png')



if OBJECT_ID ('upload_music') is not null
drop table upload_music
go
--上传音乐表
create table upload_music
(
	u_id varchar(8) not null,
	u_name varchar(18) not null,
	upload_time date not null,
	a_id varchar(8) not null,
	a_name varchar(18) not null,
	m_singer varchar(18) not null,
	par_id varchar(100) not null,
	par_name varchar(100) not null,
	par_lyric varchar(100) not null,
	music_address varchar(100) not null,
	song_time varchar(12) not null,
	cd_id varchar(8) not null,
	cd_name varchar(100) not null,

)

select * from upload_music

select Count(*) from upload_music where a_id = '10000002'

select Count(*)from (select distinct a_name from upload_music where m_singer = '许嵩') As Temp 

select top 3 *  from upload_music where a_id = '10000001' 

select * from upload_music where a_id = '10000001' and cd_id = '2'

select TOP 3 a_id,cd_name,m_singer,a_name,song_time,par_lyric,music_address,cd_id from upload_music 

select a_id,cd_name,m_singer,a_name,song_time,par_lyric,music_address,cd_id from upload_music where a_id = 10000001 and cd_id = 1

insert upload_music values ('20000001','大嵩鼠','2012-12-24','10000001','青年报社','许嵩','1001','现代','SingSonglrc\许嵩 - 雅俗共赏.lrc','SingSong\许嵩 - 雅俗共赏.flac','04:09','1','雅俗共赏')
insert upload_music values ('20000001','大嵩鼠','2012-12-24','10000001','青年报社','许嵩','1001','现代','SingSonglrc\许嵩 - 老古董.lrc','SingSong\许嵩 - 老古董.flac','04:04','2','老古董')
insert upload_music values ('20000001','大嵩鼠','2012-12-24','10000001','青年报社','许嵩','1001','现代','SingSonglrc\许嵩 - 有桃花.lrc','SingSong\许嵩 - 有桃花.flac','04:02','3','有桃花')
insert upload_music values ('20000001','大嵩鼠','2012-12-24','10000001','青年报社','许嵩','1001','现代','SingSonglrc\许嵩 - 等到烟火清凉.lrc','SingSong\许嵩 - 等到烟火清凉.flac','03:01','4','等到烟火清凉')
insert upload_music values ('20000001','大嵩鼠','2012-12-24','10000001','青年报社','许嵩','1001','现代','SingSonglrc\许嵩 - 弹指一挥间.lrc','SingSong\许嵩 - 弹指一挥间.flac','04:47','5','弹指一挥间')
insert upload_music values ('20000001','大嵩鼠','2012-12-24','10000001','青年报社','许嵩','1002','古风','SingSonglrc\许嵩 - 今年勇.lrc','SingSong\许嵩 - 今年勇.flac','03:24','6','今年勇')
insert upload_music values ('20000001','大嵩鼠','2012-12-24','10000001','青年报社','许嵩','1001','现代','SingSonglrc\许嵩 - 七夕.lrc','SingSong\许嵩 - 七夕.flac','03:53','7','七夕')
insert upload_music values ('20000001','大嵩鼠','2012-12-24','10000001','青年报社','许嵩','1001','现代','SingSonglrc\许嵩 - 山水之间.lrc','SingSong\许嵩 - 山水之间.flac','04:36','8','山水之间')
insert upload_music values ('20000001','大嵩鼠','2012-12-24','10000001','青年报社','许嵩','1001','现代','SingSonglrc\许嵩 - 梧桐灯.lrc','SingSong\许嵩 - 梧桐灯.flac','04:37','9','梧桐灯')
insert upload_music values ('20000001','大嵩鼠','2012-12-24','10000001','青年报社','许嵩','1001','现代','SingSonglrc\许嵩 - 隐隐约约.lrc','SingSong\许嵩 - 隐隐约约.flac','03:23','10','隐隐约约')

insert upload_music values ('20000001','大嵩鼠','2012-12-24','10000002','不如吃茶去','许嵩','1001','现代','SingSonglrc\许嵩 - 宇宙之大.lrc','SingSong\许嵩 - 宇宙之大.flac','04:33','1','宇宙之大')

insert upload_music values ('20000001','大嵩鼠','2012-12-24','10000003','雨幕','许嵩','1002','古风','SingSonglrc\许嵩 - 雨幕.lrc','SingSong\许嵩 - 雨幕.flac','04:00','1','雨幕')

insert upload_music values ('20000002','二谦','2012-11-24','20000001','怪咖','薛之谦','1001','现代','SingSonglrc\薛之谦 - 怪咖.lrc','SingSong\薛之谦 - 怪咖.flac','04:10','1','怪咖')
insert upload_music values ('20000002','二谦','2012-11-24','20000001','怪咖','薛之谦','1001','现代','SingSonglrc\薛之谦 - 狐狸.lrc','SingSong\薛之谦 - 狐狸.flac','03:54','2','狐狸')
insert upload_music values ('20000002','二谦','2012-11-24','20000001','怪咖','薛之谦','1001','现代','SingSonglrc\薛之谦 - 摩天大楼.lrc','SingSong\薛之谦 - 摩天大楼.flac','03:50','3','摩天大楼')
insert upload_music values ('20000002','二谦','2012-11-24','20000001','怪咖','薛之谦','1001','现代','SingSonglrc\薛之谦 - 那是你离开了北京的生活.lrc','SingSong\薛之谦 - 那是你离开了北京的生活.flac','04:28','4','那是你离开了北京的生活')
insert upload_music values ('20000002','二谦','2012-11-24','20000001','怪咖','薛之谦','1001','现代','SingSonglrc\薛之谦 - 天份.lrc','SingSong\薛之谦 - 天份.flac','04:08','5','天份')
insert upload_music values ('20000002','二谦','2012-11-24','20000001','怪咖','薛之谦','1001','现代','SingSonglrc\薛之谦 - 哑巴.lrc','SingSong\薛之谦 - 哑巴.flac','04:21','6','哑巴')
insert upload_music values ('20000002','二谦','2012-11-24','20000001','怪咖','薛之谦','1001','现代','SingSonglrc\薛之谦 - 最好.lrc','SingSong\薛之谦 - 最好.flac','04:15','7','最好')
insert upload_music values ('20000002','二谦','2012-11-24','20000001','怪咖','薛之谦','1001','现代','SingSonglrc\薛之谦 - 肆无忌惮.lrc','SingSong\薛之谦 - 肆无忌惮.flac','04:19','8','肆无忌惮')

insert upload_music values ('20000002','二谦','2012-11-24','20000002','绅士','薛之谦','1001','现代','SingSonglrc\薛之谦 - 演员.lrc','SingSong\薛之谦 - 演员.flac','04:21','1','演员')

insert upload_music values ('20000002','二谦','2012-11-24','20000003','渡','薛之谦','1001','现代','SingSonglrc\薛之谦 - 火星人来过.lrc','SingSong\薛之谦 - 火星人来过.flac','03:36','1','火星人来过')

insert upload_music values ('20000002','二谦','2012-11-24','20000004','一半','薛之谦','1001','现代','SingSonglrc\薛之谦 - 一半.lrc','SingSong\薛之谦 - 一半.flac','04:46','1','一半')


select cd_name,m_singer,a_name,song_time,par_lyric,music_address from upload_music



if OBJECT_ID ('partition') is not null
drop table partition
go
--分区(用于区分音乐)
create table partition
(
	par_id varchar(100) not null unique,
	par_name varchar(100) not null,
)

select par_name from partition where par_id = '1002'

insert partition values('1001','现代')
insert partition values('1002','古风')
insert partition values('1003','摇滚')
insert partition values('1004','电子')
insert partition values('1005','另类独立')
insert partition values('1006','轻音乐')
insert partition values('1007','影视原声')
insert partition values('1008','ACG')
insert partition values('1009','怀旧')


if OBJECT_ID ('singer') is not null
drop table singer
go
--歌手表
create table singer
(
	u_id varchar(8) not null unique,
	s_id varchar(8) not null unique,
	s_type varchar(10) not null,
	s_sex varchar(8)not null,
	s_name varchar(18) not null,
	cd_u int not null,
	album_u int not null,
	mv_u int not null,
	s_pic varchar(400)not null,
	s_info varchar(400),
)

select * from singer where s_name='许嵩'
select * from singer where s_sex='男歌手'

insert singer values ('20000001','s_100001','华语','男歌手','许嵩','0','0','0','SingerIMG\许嵩.png','大家好，我是许嵩，以后也请大家多多关照。')
insert singer values ('20000002','s_100002','华语','男歌手','薛之谦','0','0','0','SingerIMG\薛之谦.png','神经病啊')
insert singer values ('20000003','s_100003','日本','男歌手','米津玄師','0','0','0','SingerIMG\米津玄師.png','大家好')
insert singer values ('20000004','s_100004','欧美','女歌手','Billie Eilish','0','0','0','SingerIMG\Billie Eilish.png','bad gay')
insert singer values ('20000005','s_100005','华语','男歌手','华晨宇','0','0','0','SingerIMG\华晨宇.png','大家好我是花花')
insert singer values ('20000006','s_100006','华语','乐队歌手','S.H.E','0','0','0','SingerIMG\S.H.E.png','')
insert singer values ('20000007','s_100007','华语','男歌手','Sou','0','0','0','SingerIMG\Sou.png','')
insert singer values ('20000008','s_100008','欧美','男歌手','TheFatRat','0','0','0','SingerIMG\TheFatRat.png','')
insert singer values ('20000009','s_100009','日本','男歌手','ぼくのり','0','0','0','SingerIMG\ぼくのりり.png','')
insert singer values ('20000010','s_100010','日本','男歌手','まふまふ','0','0','0','SingerIMG\まふまふ.png','')
insert singer values ('20000011','s_100011','华语','女歌手','阿达娃','0','0','0','SingerIMG\阿达娃.png','')
insert singer values ('20000012','s_100012','华语','女歌手','阿悠悠','0','0','0','SingerIMG\阿悠悠.png','')
insert singer values ('20000013','s_100013','华语','女歌手','曾轶可','0','0','0','SingerIMG\曾轶可.png','')
insert singer values ('20000014','s_100014','华语','女歌手','程梦佳','0','0','0','SingerIMG\程梦佳.png','')
insert singer values ('20000015','s_100015','日本','男歌手','川井憲次','0','0','0','SingerIMG\川井憲次.png','')
insert singer values ('20000016','s_100016','华语','女歌手','邓丽君','0','0','0','SingerIMG\邓丽君.png','')
insert singer values ('20000017','s_100017','华语','女歌手','封面囧菌','0','0','0','SingerIMG\封面囧菌.png','')
insert singer values ('20000018','s_100018','华语','女歌手','冯提莫','0','0','0','SingerIMG\冯提莫.png','')
insert singer values ('20000019','s_100019','日本','男歌手','高梨康治','0','0','0','SingerIMG\高梨康治.png','')
insert singer values ('20000020','s_100020','华语','女歌手','韩红','0','0','0','SingerIMG\韩红.png','')
insert singer values ('20000021','s_100021','日本','男歌手','和田光司','0','0','0','SingerIMG\和田光司.png','')
insert singer values ('20000022','s_100022','华语','女歌手','花粥','0','0','0','SingerIMG\花粥.png','')
insert singer values ('20000023','s_100023','华语','女歌手','Aki阿杰','0','0','0','SingerIMG\Aki阿杰.png','歌手，以厚重有质感的御姐音成名。 代表作：《挑兰灯》、《东风志》、《闲云志》、《击鼓》等。')
insert singer values ('20000024','s_100024','华语','女歌手','黄师扶','0','0','0','SingerIMG\黄师扶.png','')
insert singer values ('20000025','s_100025','日本','男歌手','菅田将暉','0','0','0','SingerIMG\菅田将暉.png','')
insert singer values ('20000026','s_100026','日本','男歌手','菅野祐悟','0','0','0','SingerIMG\菅野祐悟.png','')
insert singer values ('20000027','s_100027','华语','男歌手','焦迈奇','0','0','0','SingerIMG\焦迈奇.png','')
insert singer values ('20000028','s_100028','华语','男歌手','李荣浩','0','0','0','SingerIMG\李荣浩.png','')
insert singer values ('20000029','s_100029','华语','女歌手','梁咏琪','0','0','0','SingerIMG\梁咏琪.png','')
insert singer values ('20000030','s_100030','华语','男歌手','林志炫','0','0','0','SingerIMG\林志炫.png','')
insert singer values ('20000031','s_100031','华语','女歌手','洛天依','0','0','0','SingerIMG\洛天依.png','')
insert singer values ('20000032','s_100032','华语','男歌手','毛不易','0','0','0','SingerIMG\毛不易.png','')
insert singer values ('20000033','s_100033','日本','男歌手','DAISHI DANCE','0','0','0','SingerIMG\DAISHI DANCE.png','')
insert singer values ('20000034','s_100034','华语','男歌手','排骨教主','0','0','0','SingerIMG\排骨教主.png','')
insert singer values ('20000035','s_100035','华语','男歌手','三无MarBlue','0','0','0','SingerIMG\三无MarBlue.png','')
insert singer values ('20000036','s_100036','华语','男歌手','沈以诚','0','0','0','SingerIMG\沈以诚.png','')
insert singer values ('20000037','s_100037','华语','女歌手','双笙','0','0','0','SingerIMG\双笙.png','')
insert singer values ('20000038','s_100038','华语','女歌手','谭维维','0','0','0','SingerIMG\谭维维.png','')
insert singer values ('20000039','s_100039','华语','女歌手','王心林','0','0','0','SingerIMG\王心林.png','')
insert singer values ('20000040','s_100040','华语','男歌手','萧亚轩','0','0','0','SingerIMG\萧亚轩.png','')
insert singer values ('20000041','s_100041','华语','男歌手','小魂','0','0','0','SingerIMG\小魂.png','')
insert singer values ('20000042','s_100042','华语','男歌手','徐秉龙','0','0','0','SingerIMG\徐秉龙.png','')
insert singer values ('20000043','s_100043','华语','男歌手','徐梦圆','0','0','0','SingerIMG\徐梦圆.png','')
insert singer values ('20000044','s_100044','欧美','女歌手','DJ Okawari','0','0','0','SingerIMG\DJ Okawari.png','')
insert singer values ('20000045','s_100045','华语','男歌手','en','0','0','0','SingerIMG\en.png','')
insert singer values ('20000046','s_100046','华语','男歌手','银临','0','0','0','SingerIMG\银临.png','')
insert singer values ('20000047','s_100047','华语','男歌手','永彬Ryan.B','0','0','0','SingerIMG\永彬Ryan.B.png','')
insert singer values ('20000048','s_100048','日本','男歌手','澤野弘之','0','0','0','SingerIMG\澤野弘之.png','')
insert singer values ('20000049','s_100049','华语','男歌手','周柏豪','0','0','0','SingerIMG\周柏豪.png','')
insert singer values ('20000050','s_100050','华语','男歌手','周杰伦','0','0','0','SingerIMG\周杰伦.png','')

select * from singer where s_name = '周杰伦'
select s_name from singer

select * from singer

if OBJECT_ID ('upload_music') is not null

drop table upload_music
go
--上传音乐表
create table upload_music
(
	u_id varchar(8) not null unique,
	u_name varchar(18) not null,
	upload_time varchar(18) not null,
	a_id varchar(8) not null unique,
	a_name varchar(18) not null,
	m_singer varchar(18) not null,
	par_id varchar(8) not null unique,
	par_name varchar(18) not null,
	par_lyric varchar(100) not null,
	music_address varchar(100) not null,

)


 